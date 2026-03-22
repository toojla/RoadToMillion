param location string
param appName string
param planId string
param appInsightsConnectionString string
@secure()
param dbConnectionString string
param tags object = {}

resource api 'Microsoft.Web/sites@2024-04-01' = {
  name: appName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: planId
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      // Note: alwaysOn is not supported on the Free (F1) tier.
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          // Matches AddNpgsqlDbContext<AppDbContext>("roadtomilliondb") in ServiceCollectionExtensions.
          name: 'ConnectionStrings__roadtomilliondb'
          value: dbConnectionString
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          // TODO: update AddCorsPolicy in ServiceCollectionExtensions to read origins from config.
          name: 'AllowedOrigins__0'
          value: ''
        }
      ]
    }
  }
}

output apiUrl string = 'https://${api.properties.defaultHostName}'
output principalId string = api.identity.principalId
