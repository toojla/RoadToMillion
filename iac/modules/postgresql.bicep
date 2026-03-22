param location string
param serverName string
param dbName string
param adminUser string
@secure()
param adminPassword string
param tags object = {}

// Burstable B1ms: 1 vCore, 2 GB RAM — cheapest Flexible Server tier (~$12/month).
resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: serverName
  location: location
  tags: tags
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    administratorLogin: adminUser
    administratorLoginPassword: adminPassword
    version: '16'
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: server
  name: dbName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// Allow Azure-hosted services to reach the server (0.0.0.0 -> 0.0.0.0 is the Azure services rule).
resource firewallAllowAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: server
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output fqdn string = server.properties.fullyQualifiedDomainName

@secure()
output connectionString string = 'Host=${server.properties.fullyQualifiedDomainName};Database=${dbName};Username=${adminUser};Password=${adminPassword};SslMode=Require'
