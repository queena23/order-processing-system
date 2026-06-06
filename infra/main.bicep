@description('Environment name')
param environment string = 'dev'

@description('Azure region')
param location string = resourceGroup().location

@description('Unique suffix for resource names')
param uniqueSuffix string = uniqueString(resourceGroup().id)

// ── Variables ──────────────────────────────────────────────────────────────
var containerRegistryName = 'orderscr${uniqueSuffix}'
var containerAppsEnvName = 'orders-${environment}-cae'
var containerAppName = 'orders-${environment}-worker'
var orderApiFunctionName = 'orders-${environment}-api-${uniqueSuffix}'
var fraudCheckerFunctionName = 'orders-${environment}-fraud-${uniqueSuffix}'
var storageName = 'orders${uniqueSuffix}'
var appInsightsName = 'orders-${environment}-insights'
var logAnalyticsName = 'orders-${environment}-logs'

// ── Log Analytics ──────────────────────────────────────────────────────────
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ── Application Insights ───────────────────────────────────────────────────
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ── Storage (needed by Functions) ──────────────────────────────────────────
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
}

// ── Container Registry ─────────────────────────────────────────────────────
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: containerRegistryName
  location: location
  sku: { name: 'Basic' }
  properties: { adminUserEnabled: true }
}

// ── Container Apps Environment ─────────────────────────────────────────────
resource containerAppsEnv 'Microsoft.App/managedEnvironments@2023-11-02-preview' = {
  name: containerAppsEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ── Order Worker Container App ─────────────────────────────────────────────
resource orderWorkerApp 'Microsoft.App/containerApps@2023-11-02-preview' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppsEnv.id
    configuration: {
      registries: [
        {
          server: containerRegistry.properties.loginServer
          username: containerRegistry.listCredentials().username
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: [
        {
          name: 'registry-password'
          value: containerRegistry.listCredentials().passwords[0].value
        }
        {
          name: 'service-bus-connection'
          value: '<your-service-bus-connection-string>'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'order-worker'
          image: '${containerRegistry.properties.loginServer}/order-worker:latest'
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'SERVICE_BUS_CONNECTION_STRING', secretRef: 'service-bus-connection' }
            { name: 'SERVICE_BUS_PROCESSING_QUEUE', value: 'orders-processing' }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 5
      }
    }
  }
}

// ── Function App — OrderApi ────────────────────────────────────────────────
resource orderApiPlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${orderApiFunctionName}-plan'
  location: location
  sku: { name: 'Y1', tier: 'Dynamic' }
  kind: 'functionapp'
}

resource orderApiFunction 'Microsoft.Web/sites@2023-01-01' = {
  name: orderApiFunctionName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: orderApiPlan.id
    siteConfig: {
      appSettings: [
        { name: 'AzureWebJobsStorage', value: 'DefaultEndpointsProtocol=https;AccountName=${storageName};AccountKey=${storageAccount.listKeys().keys[0].value}' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'COSMOS_CONNECTION_STRING', value: ' <your-cosmos-connection-string>' }
        { name: 'COSMOS_DATABASE_NAME', value: 'OrdersDb' }
        { name: 'COSMOS_CONTAINER_NAME', value: 'orders' }
        { name: 'SERVICE_BUS_CONNECTION_STRING', value: '<your-service-bus-connection-string>' }
        { name: 'SERVICE_BUS_ORDERS_QUEUE', value: 'orders' }
        { name: 'APPINSIGHTS_INSTRUMENTATIONKEY', value: appInsights.properties.InstrumentationKey }
      ]
    }
  }
}

// ── Function App — FraudChecker ────────────────────────────────────────────
resource fraudCheckerPlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${fraudCheckerFunctionName}-plan'
  location: location
  sku: { name: 'Y1', tier: 'Dynamic' }
  kind: 'functionapp'
}

resource fraudCheckerFunction 'Microsoft.Web/sites@2023-01-01' = {
  name: fraudCheckerFunctionName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: fraudCheckerPlan.id
    siteConfig: {
      appSettings: [
        { name: 'AzureWebJobsStorage', value: 'DefaultEndpointsProtocol=https;AccountName=${storageName};AccountKey=${storageAccount.listKeys().keys[0].value}' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'SERVICE_BUS_CONNECTION_STRING', value: '<your-service-bus-connection-string>' }
        { name: 'SERVICE_BUS_ORDERS_QUEUE', value: 'orders' }
        { name: 'SERVICE_BUS_PROCESSING_QUEUE', value: 'orders-processing' }
        { name: 'APPINSIGHTS_INSTRUMENTATIONKEY', value: appInsights.properties.InstrumentationKey }
      ]
    }
  }
}

// ── Outputs ────────────────────────────────────────────────────────────────
output containerRegistryLoginServer string = containerRegistry.properties.loginServer
output orderApiFunctionName string = orderApiFunction.name
output fraudCheckerFunctionName string = fraudCheckerFunction.name