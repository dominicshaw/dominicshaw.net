targetScope = 'subscription'

@description('Azure region used for the Function App and monitoring resources.')
param location string = 'uksouth'

@description('Azure region used for the Static Web Apps control resource. Content is served globally.')
param staticWebAppLocation string = 'westeurope'

@description('Azure Communication Services data residency region.')
param communicationDataLocation string = 'Europe'

@description('Email address that receives website enquiries.')
param contactRecipientAddress string = 'dominicshaw@hotmail.com'

@description('DNS zone created in Azure without changing public delegation.')
param domainName string = 'dominicshaw.net'

var resourceGroupName = 'rg-dominicshaw-prod-${location}'

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: {
    application: domainName
    environment: 'production'
    managedBy: 'bicep'
  }
}

module production './resources.bicep' = {
  name: 'dominicshaw-production'
  scope: resourceGroup
  params: {
    location: location
    staticWebAppLocation: staticWebAppLocation
    communicationDataLocation: communicationDataLocation
    contactRecipientAddress: contactRecipientAddress
    domainName: domainName
  }
}

output resourceGroupName string = resourceGroup.name
output staticWebAppName string = production.outputs.staticWebAppName
output staticWebAppHostname string = production.outputs.staticWebAppHostname
output staticWebAppResourceId string = production.outputs.staticWebAppResourceId
output functionAppName string = production.outputs.functionAppName
output functionAppHostname string = production.outputs.functionAppHostname
output functionAppResourceId string = production.outputs.functionAppResourceId
output communicationServiceName string = production.outputs.communicationServiceName
output managedSenderAddress string = production.outputs.managedSenderAddress
output dnsNameServers array = production.outputs.dnsNameServers
output githubClientId string = production.outputs.githubClientId
output tenantId string = tenant().tenantId
output subscriptionId string = subscription().subscriptionId
