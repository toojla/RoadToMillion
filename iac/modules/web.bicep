param location string
param appName string
param apiUrl string
param tags object = {}

// Blazor WASM compiles to static files — Static Web App is the correct host (Free tier).
resource web 'Microsoft.Web/staticSites@2024-04-01' = {
  name: appName
  location: location
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

resource webAppSettings 'Microsoft.Web/staticSites/config@2024-04-01' = {
  parent: web
  name: 'appsettings'
  properties: {
    // Matches builder.Configuration["ApiBaseUrl"] in RoadToMillion.Web/Program.cs.
    ApiBaseUrl: apiUrl
  }
}

output defaultHostname string = web.properties.defaultHostname
