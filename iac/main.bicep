targetScope = 'subscription'

@description('Primary Azure region for all resources.')
param location string = 'swedencentral'

@description('Location for Static Web App (limited region support).')
param webLocation string = 'westeurope'

@description('Name of the resource group to create.')
param resourceGroupName string

@description('Tags applied to all resources.')
param tags object = {}

// Observability
param workspaceName string
param appInsightsName string

// App Service Plan
param planName string

// PostgreSQL
param postgresServerName string
param postgresDbName string
param postgresAdminUser string
@secure()
param postgresAdminPassword string
param postgresAllowedIpAddresses array = []

// API App Service
param apiAppName string

// Static Web App
param webAppName string

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module observability 'modules/observability.bicep' = {
  scope: rg
  params: {
    location: location
    workspaceName: workspaceName
    appInsightsName: appInsightsName
    tags: tags
  }
}

module plan 'modules/app-service-plan.bicep' = {
  scope: rg
  params: {
    location: location
    planName: planName
    tags: tags
  }
}

module postgres 'modules/postgresql.bicep' = {
  scope: rg
  params: {
    location: location
    serverName: postgresServerName
    dbName: postgresDbName
    adminUser: postgresAdminUser
    adminPassword: postgresAdminPassword
    allowedIpAddresses: postgresAllowedIpAddresses
    tags: tags
  }
}

module api 'modules/api.bicep' = {
  scope: rg
  params: {
    location: location
    appName: apiAppName
    planId: plan.outputs.planId
    appInsightsConnectionString: observability.outputs.appInsightsConnectionString
    dbConnectionString: postgres.outputs.connectionString
    tags: tags
  }
}

module web 'modules/web.bicep' = {
  scope: rg
  params: {
    location: webLocation
    appName: webAppName
    apiUrl: api.outputs.apiUrl
    tags: tags
  }
}

output apiUrl string = api.outputs.apiUrl
output webUrl string = web.outputs.defaultHostname
