@description('Name of the resource group')
param resourceGroupName string = 'rg-${take(uniqueString(subscription().id, resourceGroup().location), 8)}'

@description('Azure location')
param location string = resourceGroup().location

resource rg 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: resourceGroupName
  location: location
  tags: {
    managedBy: 'bicep'
    environment: 'shared'
  }
}

output resourceGroupId string = rg.id
