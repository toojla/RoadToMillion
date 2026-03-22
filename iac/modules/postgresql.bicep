param location string
param serverName string
param dbName string
param adminUser string
@secure()
param adminPassword string
param entraAdminObjectId string
param entraAdminName string
// List of IP addresses (single IPs or CIDR ranges) allowed to connect directly,
// e.g. for local development or DBA access. Each entry becomes a firewall rule.
param allowedIpAddresses array = []
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
    authConfig: {
      // Keep password auth enabled for DBA access; add Entra ID for managed identity.
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Enabled'
      tenantId: tenant().tenantId
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

// One firewall rule per entry in allowedIpAddresses.
resource firewallAllowedIps 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = [
  for (ip, i) in allowedIpAddresses: {
    parent: server
    name: 'AllowIp-${i}'
    properties: {
      startIpAddress: ip
      endIpAddress: ip
    }
  }
]

// Register an Entra ID administrator who can connect and run post-deploy SQL
// (e.g. pgaadauth_create_principal_with_oid to register the API managed identity).
resource entraAdmin 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2024-08-01' = {
  parent: server
  name: entraAdminObjectId
  properties: {
    principalName: entraAdminName
    principalType: 'ServicePrincipal'
    tenantId: tenant().tenantId
  }
}

output fqdn string = server.properties.fullyQualifiedDomainName
