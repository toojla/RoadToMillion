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

// App Services
param apiAppName = 'app-roadtomillion-api-001-prod'

// Static Web App
param webAppName = 'stapp-roadtomillion-web-001-prod'
