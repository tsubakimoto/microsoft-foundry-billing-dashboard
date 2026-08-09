param location string
param namePrefix string
param planSku string
param runtime string
param appSettings object

var planName = '${namePrefix}-api-plan'
var apiAppName = '${namePrefix}-api'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
    name: planSku
    tier: planSku
  }
  properties: {
    reserved: true
  }
}

resource apiApp 'Microsoft.Web/sites@2023-12-01' = {
  name: apiAppName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: runtime
      alwaysOn: true
      appSettings: [
        for settingName in objectKeys(appSettings): {
          name: settingName
          value: string(appSettings[settingName])
        }
      ]
    }
  }
}

output apiUrl string = 'https://${apiApp.properties.defaultHostName}'
