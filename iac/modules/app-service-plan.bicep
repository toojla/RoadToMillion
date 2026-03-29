param location string
param planName string
param tags object = {}

// F1 is the Free tier: 60 CPU min/day, 1 GB storage, shared infra, no custom domains.
resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  tags: tags
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  kind: 'linux'
  properties: {
    reserved: true // required for Linux
  }
}

output planId string = plan.id
