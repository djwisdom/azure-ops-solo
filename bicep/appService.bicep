@description('App Service plan name')
param appServicePlanName string

@description('Web app name')
param webAppName string

@description('Azure location')
param location string = resourceGroup().location

@description('Key Vault resource ID for identity reference')
param keyVaultId string

@description('SKU tier')
param skuTier string = 'Standard'

@description('SKU name')
param skuName string = 'S1'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: skuName
    tier: skuTier
  }
  kind: 'app'
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      httpLoggingEnabled: true
      detailedErrorLoggingEnabled: true
      appSettings: [
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
  }
}

// Reference Key Vault secrets via Key Vault reference (no secrets in Bicep)
resource secretRef 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: split(keyVaultId, '/')[8]
}

output webAppId string = webApp.id
output webAppUri string = 'https://${webAppName}.azurewebsites.net/'
output managedIdentityPrincipalId string = webApp.identity.principalId
