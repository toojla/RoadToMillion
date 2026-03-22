using './main.bicep'

param location = 'swedencentral'
param webLocation = 'westeurope'

param resourceGroupName = 'rg-roadtomillion-prod'

param tags = {
  environment: 'production'
  project: 'RoadToMillion'
  managedBy: 'bicep'
}

// Observability
param workspaceName = 'log-roadtomillion-prod'
param appInsightsName = 'appi-roadtomillion-prod'

// App Service Plan (Free tier F1)
param planName = 'asp-roadtomillion-prod'

// PostgreSQL Flexible Server (Burstable B1ms — cheapest tier)
param postgresServerName = 'psql-roadtomillion-001-prod'
param postgresDbName = 'roadtomilliondb'
param postgresAdminUser = 'rtmadmin'
// Read from the POSTGRES_ADMIN_PASSWORD environment variable at deploy time.
// Locally: $env:POSTGRES_ADMIN_PASSWORD = '<password>'
// GitHub Actions: set via POSTGRES_ADMIN_PASSWORD env var on the workflow step.
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD')

// Object ID and display name of the Entra ID principal that will be registered
// as the PostgreSQL Entra admin (used to run post-deploy SQL for managed identity).
// Typically the deploying service principal: az ad sp show --id <appId> --query id -o tsv
param postgresEntraAdminObjectId = 'd45536c3-45bb-4256-82c9-d3a33ad44cca'
param postgresEntraAdminName = 'sp-roadtomillion-deploy'

// IP addresses allowed to connect directly to PostgreSQL (e.g. for DBA/local dev access).
// Add your public IP here. Find it with: curl ifconfig.me
param postgresAllowedIpAddresses = [
  '81.226.21.67'
]

// App Services
param apiAppName = 'app-roadtomillion-api-001-prod'

// Static Web App
param webAppName = 'stapp-roadtomillion-web-001-prod'
