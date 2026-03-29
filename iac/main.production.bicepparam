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


// IP addresses allowed to connect directly to PostgreSQL (e.g. for DBA/local dev access).
// Add your public IP here. Find it with: curl ifconfig.me
param postgresAllowedIpAddresses = [
  '81.226.21.67'
]

// App Services
param apiAppName = 'app-roadtomillion-api-001-prod'

// Static Web App
param webAppName = 'stapp-roadtomillion-web-001-prod'

// CORS: set to the Static Web App default hostname after first deploy.
// Get it with: az staticwebapp show --name stapp-roadtomillion-web-001-prod --resource-group rg-roadtomillion-prod --query defaultHostname -o tsv
param webAppHostname = 'mango-mud-0cad3dd03.1.azurestaticapps.net'
