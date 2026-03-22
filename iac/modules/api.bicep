param location string
param appName string
param planId string
param appInsightsConnectionString string
@secure()
param dbConnectionString string
@description('Allowed CORS origin for the API (Static Web App URL). Empty string disables the setting so the API falls back to its appsettings.Development.json default.')
param allowedOrigin string = ''
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
          // Picked up by AddCorsPolicy via IConfiguration. Set to the Static Web App hostname.
          name: 'AllowedOrigins__0'
          value: allowedOrigin
        }
      ]
    }
  }
}

output apiUrl string = 'https://${api.properties.defaultHostName}'
output principalId string = api.identity.principalId
