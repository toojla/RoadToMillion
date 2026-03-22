param location string
param appName string
param planId string
param appInsightsConnectionString string
@secure()
param dbConnectionString string
@description('Allowed CORS origin for the API (Static Web App URL). Empty string disables the setting so the API falls back to its appsettings.Development.json default.')
param allowedOrigin string = ''
@description('IP addresses allowed to reach the App Service (main site and SCM). When non-empty, all other traffic is denied. NOTE: restricting SCM also blocks GitHub Actions runners from deploying via azure/webapps-deploy — deploy from an allowed IP or via VPN when this is set.')
param allowedIpAddresses array = []
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
      // Deny all traffic except explicitly allowed IPs. Empty array = no restrictions (allow all).
      ipSecurityRestrictionsDefaultAction: empty(allowedIpAddresses) ? 'Allow' : 'Deny'
      ipSecurityRestrictions: [for (ip, i) in allowedIpAddresses: {
        action: 'Allow'
        ipAddress: '${ip}/32'
        priority: 100 + i * 10
        name: 'Allow-ip-${i}'
        description: ip
      }]
      // Apply the same IP rules to the Kudu/SCM site.
      scmIpSecurityRestrictionsUseMain: true
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
