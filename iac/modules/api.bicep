param location string
param appName string
param planId string
param appInsightsConnectionString string
param dbServerFqdn string
param dbName string
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
          // Password-free connection string; the API uses Azure AD token provider at runtime.
          // Username must match the role registered via pgaadauth_create_principal_with_oid.
          name: 'ConnectionStrings__roadtomilliondb'
          value: 'Host=${dbServerFqdn};Database=${dbName};Username=${appName};SslMode=Require'
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
