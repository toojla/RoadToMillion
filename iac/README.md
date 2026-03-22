# Infrastructure as Code

Bicep templates for deploying RoadToMillion to Azure at subscription scope.

## Prerequisites

- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) 2.60+
- [Bicep CLI](https://learn.microsoft.com/azure/azure-resource-manager/bicep/install) 0.28+ (or install via `az bicep install`)
- An Azure subscription with **Owner** or **Contributor + User Access Administrator** role

## Folder structure

```
iac/
├── main.bicep                      # Entry point — subscription scope
├── main.production.bicepparam      # Production parameter values
└── modules/
    ├── observability.bicep         # Log Analytics Workspace + Application Insights
    ├── app-service-plan.bicep      # Linux App Service Plan (Free F1)
    ├── postgresql.bicep            # PostgreSQL Flexible Server (Burstable B1ms)
    ├── api.bicep                   # App Service for the .NET API
    └── web.bicep                   # Static Web App for the Blazor WASM frontend
```

## 1. Authenticate

### Interactive (local development)

```powershell
az login
az account set --subscription "<your-subscription-id>"
```

Verify the correct subscription is active:

```powershell
az account show --query "{name:name, id:id, state:state}"
```

### Service principal (CI/CD — GitHub Actions)

The workflow uses **OpenID Connect (OIDC)** — no long-lived client secrets stored in GitHub.

**Step 1 — Create an app registration and service principal:**

```powershell
az ad app create --display-name "sp-roadtomillion-deploy"
# Note the appId from the output
```

**Step 2 — Assign Contributor at subscription scope:**

```powershell
$appId = "<appId from above>"
$subscriptionId = "<your-subscription-id>"
$objectId = az ad sp show --id $appId --query id -o tsv

az role assignment create `
  --assignee-object-id $objectId `
  --assignee-principal-type ServicePrincipal `
  --role Contributor `
  --scope "/subscriptions/$subscriptionId"
```

**Step 3 — Add a federated credential for GitHub Actions:**

```powershell
$appObjectId = az ad app show --id $appId --query id -o tsv

az ad app federated-credential create `
  --id $appObjectId `
  --parameters '{
    "name": "github-actions-production",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<your-github-org>/<your-repo>:environment:production",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

> The `subject` must exactly match the GitHub environment name used in the workflow (`production`). If you add more environments, add a federated credential for each.

**Step 4 — Add GitHub Actions secrets** (Settings → Secrets and variables → Actions):

| Secret name | Value |
|---|---|
| `AZURE_CLIENT_ID` | The `appId` from Step 1 |
| `AZURE_TENANT_ID` | Your Azure AD tenant ID (`az account show --query tenantId -o tsv`) |
| `AZURE_SUBSCRIPTION_ID` | Your subscription ID |
| `POSTGRES_ADMIN_PASSWORD` | The strong password for the PostgreSQL admin user |

**Step 5 — Create a `production` GitHub Environment** (Settings → Environments):

Add required reviewers so the deploy job pauses for approval after the what-if output is reviewed.

## 2. Validate & compile locally

Build the template to ARM JSON and check for errors — no Azure login required:

```powershell
bicep build iac/main.bicep --stdout
```

The `--stdout` flag prints the compiled ARM JSON to the terminal instead of writing a file, keeping the workspace clean. A clean build exits with code `0` and no error output.

To lint for best-practice violations as well:

```powershell
bicep lint iac/main.bicep
```

To validate the full parameter file against the template (requires login, but makes no changes):

```powershell
az deployment sub validate `
  --location swedencentral `
  --template-file iac/main.bicep `
  --parameters iac/main.production.bicepparam `
  --parameters postgresAdminPassword=$env:PG_PASSWORD
```

## 3. Set the database password

The PostgreSQL admin password is the only secret that must be supplied at deploy time — it is intentionally absent from the parameter file.

Store it securely and never commit it to source control:

```powershell
# PowerShell — set for the current session
$env:PG_PASSWORD = "<strong-password>"
```

Password requirements: 8–128 characters, must include uppercase, lowercase, and a digit or symbol.

## 4. Deploy

Run from the repository root (where `RoadToMillion.slnx` lives):

```powershell
az deployment sub create `
  --location swedencentral `
  --template-file iac/main.bicep `
  --parameters iac/main.production.bicepparam `
  --parameters postgresAdminPassword=$env:PG_PASSWORD `
  --name "deploy-roadtomillion-prod"
```

### What-if (dry run)

Preview changes without deploying anything:

```powershell
az deployment sub what-if `
  --location swedencentral `
  --template-file iac/main.bicep `
  --parameters iac/main.production.bicepparam `
  --parameters postgresAdminPassword=$env:PG_PASSWORD
```

## 5. After deployment

The deployment outputs the live URLs:

```powershell
az deployment sub show `
  --name "deploy-roadtomillion-prod" `
  --query "properties.outputs"
```

| Output | Description |
|---|---|
| `apiUrl` | HTTPS URL of the API App Service |
| `webUrl` | HTTPS URL of the Static Web App |

## GitHub Actions workflow

The workflow is at [`.github/workflows/deploy-infrastructure.yml`](../.github/workflows/deploy-infrastructure.yml) and is triggered manually via **Actions → Deploy Infrastructure → Run workflow**.

**Inputs:**
| Input | Description |
|---|---|
| `environment` | Target environment (`production`) |
| `what_if` | When `true`, runs lint + validate + what-if but stops before deploying |

**Job sequence:**
```
validate → what-if → deploy (pauses for environment approval)
```

Setting `what_if: true` is useful to preview what ARM would change before committing to a real deploy.

## 6. Post-deploy: register the API managed identity in PostgreSQL

The PostgreSQL `administrators` resource does not support what-if validation and is intentionally excluded from Bicep. Entra ID admin registration and the managed identity role are set up once via CLI and SQL after the first deploy.

**Step 1 — Register the Entra ID admin on the server:**

```powershell
# The principal registered here can connect via Entra token to run SQL in Step 2.
# This is typically your deploying service principal or your own user account.
$spObjectId = az ad sp show --id "<appId-of-sp-roadtomillion-deploy>" --query id -o tsv

az postgres flexible-server microsoft-entra-admin create `
  --resource-group rg-roadtomillion-prod `
  --server-name psql-roadtomillion-001-prod `
  --display-name "sp-roadtomillion-deploy" `
  --object-id $spObjectId `
  --type ServicePrincipal
```

**Step 2 — Retrieve the API's managed identity object ID:**

```powershell
$apiPrincipalId = az webapp identity show `
  --name app-roadtomillion-api-001-prod `
  --resource-group rg-roadtomillion-prod `
  --query principalId -o tsv
```

**Step 3 — Connect to PostgreSQL as the Entra admin:**

```powershell
$token = az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv

psql "host=psql-roadtomillion-001-prod.postgres.database.azure.com \
      port=5432 \
      dbname=roadtomilliondb \
      user=<your-entra-principal-upn-or-sp-name> \
      password=$token \
      sslmode=require"
```

**Step 4 — Register the managed identity as a PostgreSQL role:**

```sql
-- Replace <api-principal-id> with the value from Step 2.
-- Username must match the App Service name (used in the connection string).
SELECT pgaadauth_create_principal_with_oid(
  'app-roadtomillion-api-001-prod',
  '<api-principal-id>',
  'service', false, false
);

GRANT ALL PRIVILEGES ON DATABASE roadtomilliondb TO "app-roadtomillion-api-001-prod";
GRANT ALL ON SCHEMA public TO "app-roadtomillion-api-001-prod";
```

This is a **one-time operation**. Re-running the Bicep deploy does not affect these PostgreSQL roles.

## Notes

- **CORS** — the API's allowed origin is currently hardcoded to `localhost`. Update `AddCorsPolicy` in [src/RoadToMillion.Api/Configuration/ServiceCollectionExtensions.cs](../src/RoadToMillion.Api/Configuration/ServiceCollectionExtensions.cs) to read from the `AllowedOrigins__0` app setting before the first production deploy.
- **F1 Free tier limits** — 60 CPU minutes/day, no Always On (app idles between requests). Upgrade to **B1** to remove these limits.
- **Static Web App deployment token** — the Blazor WASM frontend is deployed separately via the Static Web App GitHub Actions integration. After the infrastructure is deployed, retrieve the token: `az staticwebapp secrets list --name stapp-roadtomillion-web-prod --query "properties.apiKey"`.
