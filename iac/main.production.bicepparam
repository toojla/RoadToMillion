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
// postgresAdminPassword must be supplied at deploy time, e.g.:
//   az deployment sub create ... --parameters postgresAdminPassword=$env:PG_PASSWORD

// App Services
param apiAppName = 'app-roadtomillion-api-001-prod'

// Static Web App
param webAppName = 'stapp-roadtomillion-web-001-prod'
