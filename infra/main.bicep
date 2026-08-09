targetScope = 'resourceGroup'

@description('Resource location.')
param location string = resourceGroup().location

@description('Prefix used for resource names.')
param namePrefix string = 'foundry-billing'

@description('Static Web App SKU.')
@allowed([
  'Free'
  'Standard'
])
param staticWebAppSku string = 'Free'

@description('Enable JWT authentication at API layer.')
param authenticationRequired bool = true

@description('Entra tenant ID used for JWT authority composition.')
param authenticationTenantId string

@description('Expected audience claim for API tokens.')
param authenticationAudience string

@description('Optional explicit authority URL. Leave empty to compose from TenantId.')
param authenticationAuthority string = ''

@description('Foundry provider name. IaC deployments require Http. Sample is development-only (local appsettings).')
@allowed([
  'Http'
])
param foundryProvider string = 'Http'

@description('Endpoint used when foundryProvider=Http.')
param foundryEndpoint string = ''

@description('Default tenant ID used for Foundry HTTP provider requests.')
param foundryTenantId string = ''

@description('Default subscription ID used for Foundry HTTP provider requests.')
param foundrySubscriptionId string = ''

@description('Entra scope used by API when calling Foundry HTTP provider. Empty disables outbound bearer token acquisition.')
param foundryEntraScope string = 'https://management.azure.com/.default'

@description('HTTP provider path for project listing.')
param foundryProjectsPath string = '/projects'

@description('HTTP provider path template for billing summary.')
param foundryBillingPathTemplate string = '/projects/{projectId}/billing?period={period}'

@description('Cache duration for provider responses.')
param foundryCacheSeconds int = 30

@description('App Service plan SKU for API hosting.')
param apiPlanSku string = 'B1'

@description('Linux runtime for API hosting.')
param apiRuntime string = 'DOTNETCORE|10.0'

module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'static-web-app'
  params: {
    location: location
    name: '${namePrefix}-swa'
    sku: staticWebAppSku
  }
}

module apiHosting 'modules/api-hosting.bicep' = {
  name: 'api-hosting'
  params: {
    location: location
    namePrefix: namePrefix
    planSku: apiPlanSku
    runtime: apiRuntime
    appSettings: {
      ClientOrigin: 'https://${staticWebApp.outputs.defaultHostname}'
      Authentication__Required: string(authenticationRequired)
      Authentication__TenantId: authenticationTenantId
      Authentication__Audience: authenticationAudience
      Authentication__Authority: authenticationAuthority
      Foundry__Provider: foundryProvider
      Foundry__Endpoint: foundryEndpoint
      Foundry__TenantId: foundryTenantId
      Foundry__SubscriptionId: foundrySubscriptionId
      Foundry__EntraScope: foundryEntraScope
      Foundry__ProjectsPath: foundryProjectsPath
      Foundry__BillingPathTemplate: foundryBillingPathTemplate
      Foundry__CacheSeconds: string(foundryCacheSeconds)
    }
  }
}

output staticWebAppDefaultHostname string = staticWebApp.outputs.defaultHostname
output apiUrl string = apiHosting.outputs.apiUrl
