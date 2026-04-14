@description('Key Vault name')
param keyVaultName string

@description('Azure location')
param location string = resourceGroup().location

@description('Tenant ID')
param tenantId string

@description('Object ID of the user/service principal with access')
param accessPolicies array = []

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enableRbacAuthorization: true
    enablePurgeProtection: true
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenantId
    accessPolicies: accessPolicies
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    }
  }
}

output keyVaultId string = kv.id
output keyVaultUri string = 'https://${keyVaultName}.vault.azure.net/'
