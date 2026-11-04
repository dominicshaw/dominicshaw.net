targetScope = 'resourceGroup'

param location string
param staticWebAppLocation string
param communicationDataLocation string
param contactRecipientAddress string
param domainName string

var nameSuffix = take(uniqueString(subscription().subscriptionId, domainName), 8)
var staticWebAppName = 'dominicshaw-net-${nameSuffix}'
var functionAppName = 'func-dominicshaw-net-${nameSuffix}'
var deploymentStorageContainerName = 'app-package-${nameSuffix}'
var contactRateLimitTableName = 'contactratelimits'
var staticWebAppUrl = 'https://${staticWebApp.properties.defaultHostname}'
var managedSenderAddress = 'DoNotReply@${managedEmailDomain.properties.mailFromSenderDomain}'
var commonTags = {
  application: domainName
  environment: 'production'
  managedBy: 'bicep'
}
var contributorRoleDefinitionId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
var websiteContributorRoleDefinitionId = 'de139f84-1756-47ae-9be6-808fbbe84772'
var storageBlobDataOwnerRoleDefinitionId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageTableDataContributorRoleDefinitionId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
var monitoringMetricsPublisherRoleDefinitionId = '3913510d-42f4-4e42-8a64-420c390055eb'

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: staticWebAppLocation
  tags: commonTags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    allowConfigFileUpdates: true
    stagingEnvironmentPolicy: 'Enabled'
  }
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-dominicshaw-prod-${nameSuffix}'
  location: location
  tags: commonTags
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-dominicshaw-prod-${nameSuffix}'
  location: location
  kind: 'web'
  tags: commonTags
  properties: {
    Application_Type: 'web'
    DisableLocalAuth: true
    Flow_Type: 'Bluefield'
    IngestionMode: 'LogAnalytics'
    Request_Source: 'rest'
    RetentionInDays: 30
    WorkspaceResourceId: logAnalyticsWorkspace.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource functionStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'stdshaw${nameSuffix}'
  location: location
  kind: 'StorageV2'
  tags: commonTags
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
    publicNetworkAccess: 'Enabled'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: functionStorage
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

resource deploymentStorageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: deploymentStorageContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: functionStorage
  name: 'default'
  properties: {}
}

resource contactRateLimitTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: contactRateLimitTableName
  properties: {}
}

resource functionIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-function-dominicshaw-prod-${nameSuffix}'
  location: location
  tags: commonTags
}

resource functionStorageBlobOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(functionStorage.id, functionIdentity.id, storageBlobDataOwnerRoleDefinitionId)
  scope: functionStorage
  properties: {
    principalId: functionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      storageBlobDataOwnerRoleDefinitionId
    )
  }
}

resource functionStorageTableContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(functionStorage.id, functionIdentity.id, storageTableDataContributorRoleDefinitionId)
  scope: functionStorage
  properties: {
    principalId: functionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      storageTableDataContributorRoleDefinitionId
    )
  }
}

resource functionMetricsPublisher 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(applicationInsights.id, functionIdentity.id, monitoringMetricsPublisherRoleDefinitionId)
  scope: applicationInsights
  properties: {
    principalId: functionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      monitoringMetricsPublisherRoleDefinitionId
    )
  }
}

resource functionPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'plan-dominicshaw-prod-${nameSuffix}'
  location: location
  kind: 'functionapp'
  tags: commonTags
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  tags: commonTags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${functionIdentity.id}': {}
    }
  }
  properties: {
    clientAffinityEnabled: false
    functionAppConfig: {
      deployment: {
        storage: {
          authentication: {
            type: 'UserAssignedIdentity'
            userAssignedIdentityResourceId: functionIdentity.id
          }
          type: 'blobContainer'
          value: '${functionStorage.properties.primaryEndpoints.blob}${deploymentStorageContainer.name}'
        }
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
      scaleAndConcurrency: {
        instanceMemoryMB: 512
        maximumInstanceCount: 1
      }
    }
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    serverFarmId: functionPlan.id
    siteConfig: {
      cors: {
        allowedOrigins: [
          staticWebAppUrl
          'https://${domainName}'
          'https://www.${domainName}'
        ]
        supportCredentials: false
      }
      ftpsState: 'Disabled'
      http20Enabled: true
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
    }
  }
  dependsOn: [
    functionMetricsPublisher
    functionStorageBlobOwner
    functionStorageTableContributor
  ]
}

resource functionAppSettings 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: functionApp
  name: 'appsettings'
  properties: {
    APPLICATIONINSIGHTS_AUTHENTICATION_STRING: 'ClientId=${functionIdentity.properties.clientId};Authorization=AAD'
    APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsights.properties.ConnectionString
    AZURE_CLIENT_ID: functionIdentity.properties.clientId
    AzureWebJobsStorage__accountName: functionStorage.name
    AzureWebJobsStorage__clientId: functionIdentity.properties.clientId
    AzureWebJobsStorage__credential: 'managedidentity'
    ContactEmail__Endpoint: 'https://${communicationService.properties.hostName}'
    ContactEmail__RecipientAddress: contactRecipientAddress
    ContactEmail__SenderAddress: managedSenderAddress
    ContactRateLimit__Endpoint: functionStorage.properties.primaryEndpoints.table
    ContactRateLimit__TableName: contactRateLimitTable.name
    FUNCTIONS_EXTENSION_VERSION: '~4'
  }
}

resource functionFtpPublishingPolicy 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2022-03-01' = {
  parent: functionApp
  name: 'ftp'
  properties: {
    allow: false
  }
}

resource functionScmPublishingPolicy 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2022-03-01' = {
  parent: functionApp
  name: 'scm'
  properties: {
    allow: false
  }
}

resource emailService 'Microsoft.Communication/emailServices@2025-09-01' = {
  name: 'email-dominicshaw-prod-${nameSuffix}'
  location: 'global'
  tags: commonTags
  properties: {
    dataLocation: communicationDataLocation
  }
}

resource managedEmailDomain 'Microsoft.Communication/emailServices/domains@2025-09-01' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: 'global'
  tags: commonTags
  properties: {
    domainManagement: 'AzureManaged'
    userEngagementTracking: 'Disabled'
  }
}

resource communicationService 'Microsoft.Communication/communicationServices@2025-09-01' = {
  name: 'acs-dominicshaw-prod-${nameSuffix}'
  location: 'global'
  tags: commonTags
  properties: {
    dataLocation: communicationDataLocation
    disableLocalAuth: true
    linkedDomains: [
      managedEmailDomain.id
    ]
  }
}

resource functionCommunicationContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(communicationService.id, functionIdentity.id, contributorRoleDefinitionId)
  scope: communicationService
  properties: {
    principalId: functionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      contributorRoleDefinitionId
    )
  }
}

resource githubDeploymentIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-github-dominicshaw-prod-${nameSuffix}'
  location: location
  tags: commonTags
}

resource githubFederatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2024-11-30' = {
  parent: githubDeploymentIdentity
  name: 'github-production'
  properties: {
    audiences: [
      'api://AzureADTokenExchange'
    ]
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:dominicshaw/dominicshaw.net:environment:production'
  }
}

resource githubFunctionContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(functionApp.id, githubDeploymentIdentity.id, websiteContributorRoleDefinitionId)
  scope: functionApp
  properties: {
    principalId: githubDeploymentIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      websiteContributorRoleDefinitionId
    )
  }
}

resource githubStaticWebAppContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(staticWebApp.id, githubDeploymentIdentity.id, contributorRoleDefinitionId)
  scope: staticWebApp
  properties: {
    principalId: githubDeploymentIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      contributorRoleDefinitionId
    )
  }
}

resource dnsZone 'Microsoft.Network/dnsZones@2018-05-01' = {
  name: domainName
  location: 'global'
  tags: commonTags
}

// Keep unrecognised subdomains on DiscountASP during the post-cutover soak period.
resource legacyWildcardARecord 'Microsoft.Network/dnsZones/A@2018-05-01' = {
  parent: dnsZone
  name: '*'
  properties: {
    TTL: 3600
    ARecords: [
      {
        ipv4Address: '45.58.159.45'
      }
    ]
  }
}

resource apexAliasRecord 'Microsoft.Network/dnsZones/A@2018-05-01' = {
  parent: dnsZone
  name: '@'
  properties: {
    TTL: 3600
    targetResource: {
      id: staticWebApp.id
    }
  }
}

resource wwwCnameRecord 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = {
  parent: dnsZone
  name: 'www'
  properties: {
    TTL: 3600
    CNAMERecord: {
      cname: staticWebApp.properties.defaultHostname
    }
  }
}

resource legacyMailCnameRecord 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = {
  parent: dnsZone
  name: 'mail'
  properties: {
    TTL: 3600
    CNAMERecord: {
      cname: 'mail5009.site4now.net'
    }
  }
}

resource legacyMxRecord 'Microsoft.Network/dnsZones/MX@2018-05-01' = {
  parent: dnsZone
  name: '@'
  properties: {
    TTL: 3600
    MXRecords: [
      {
        exchange: 'igw09.site4now.net'
        preference: 10
      }
    ]
  }
}

resource legacySpfRecord 'Microsoft.Network/dnsZones/TXT@2018-05-01' = {
  parent: dnsZone
  name: '@'
  properties: {
    TTL: 3600
    TXTRecords: [
      {
        value: [
          'v=spf1 a mx include:_spf.site4now.net ?all'
        ]
      }
      {
        value: [
          '_7botitch7tg8rqppk2ls4tcps54xwyf'
        ]
      }
    ]
  }
}

resource wwwValidationRecord 'Microsoft.Network/dnsZones/TXT@2018-05-01' = {
  parent: dnsZone
  name: '_dnsauth.www'
  properties: {
    TTL: 3600
    TXTRecords: [
      {
        value: [
          '_y6ddfjxbnf29zr3yldll1rlrnx03bl0'
        ]
      }
    ]
  }
}

output staticWebAppName string = staticWebApp.name
output staticWebAppHostname string = staticWebApp.properties.defaultHostname
output staticWebAppResourceId string = staticWebApp.id
output functionAppName string = functionApp.name
output functionAppHostname string = functionApp.properties.defaultHostName
output functionAppResourceId string = functionApp.id
output communicationServiceName string = communicationService.name
output managedSenderAddress string = managedSenderAddress
output dnsNameServers array = dnsZone.properties.nameServers
output githubClientId string = githubDeploymentIdentity.properties.clientId
