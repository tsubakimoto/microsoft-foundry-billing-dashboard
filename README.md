# Microsoft Foundry Billing Dashboard (MVP)

Microsoft Foundry プロジェクトの課金状況を表示する MVP です。

- `src/Dashboard.Api`: ASP.NET Core Minimal API
- `src/Dashboard.Client`: Blazor WebAssembly (Standalone)
- `src/Dashboard.Shared`: 共有 DTO
- `tests/Dashboard.Api.Tests`: xUnit テスト
- `infra`: Azure デプロイ用 Bicep

## SDK / Target Framework

- .NET SDK: `10.0.302` (`global.json`)
- TargetFramework: `net10.0`

## ローカル実行

```bash
dotnet restore Dashboard.sln
dotnet build Dashboard.sln
dotnet test Dashboard.sln
```

API:

```bash
dotnet run --project src/Dashboard.Api
```

Client:

```bash
dotnet run --project src/Dashboard.Client
```

## API

- `GET /api/projects`
- `GET /api/projects/{projectId}/billing?period=currentMonth|previousMonth`

`period` は `currentMonth` / `previousMonth` のみ受け付けます。

## 認証 (Client + API)

### API (`src/Dashboard.Api/appsettings*.json`)

- `Authentication:Required=true` の場合、`/api/*` は認証必須
- `Authentication:Required=false` は Development 環境のみ許可（本番で起動失敗）

### Client (`src/Dashboard.Client/wwwroot/appsettings*.json`)

- `Authentication:Enabled=true` で MSAL ログインを有効化
- 画面上部にログイン/ログアウト UI と認証状態を表示
- API 呼び出し時にアクセストークンを付与
- Development 既定は `Authentication:Enabled=false` の Sample モード

## Client API URL 設定

- 本番既定: `Api:BaseUrl` は `/`（相対 URL）
- Development: `https://localhost:7282/`（API ローカル URL）
- `Api:BaseUrl` は絶対 URL または相対パスを受け付け

## Foundry HTTP Provider 契約

`Foundry:Provider=Http` のとき:

- `Foundry:Endpoint` 必須（絶対 URI）
- `tenantId` / `subscriptionId` はクエリまたはヘッダー指定可。未指定時は `Foundry:TenantId` / `Foundry:SubscriptionId` を使用
- `Foundry:AllowedTenantIds` / `Foundry:AllowedSubscriptionIds`（未設定時は `Foundry:TenantId` / `Foundry:SubscriptionId`）allowlist に含まれない値は 403
- `Authentication:Required=true` の場合、JWT の `tid` claim と `Foundry:TenantId` の一致を必須化（不一致は 403）
- 上記スコープ値は外部リクエストへ必ず反映
  - Query: `Foundry:TenantIdQueryParameterName`, `Foundry:SubscriptionIdQueryParameterName`
  - Header: `Foundry:TenantIdHeaderName`, `Foundry:SubscriptionIdHeaderName`
- `Foundry:EntraScope` 設定時、`DefaultAzureCredential` で取得した Entra token を外部呼び出しの Authorization ヘッダーへ付与

`Foundry:Provider=Sample` は Development 専用です。`infra/main.bicep` の既定は `Http` で、`authenticationRequired=true` では `Sample` を許可しません。

外部エンドポイント呼び出し契約:

- `GET {Endpoint}{ProjectsPath}` → `ProjectSummary[]`
- `GET {Endpoint}{BillingPathTemplate}` (`{projectId}`, `{period}` 置換後) → `BillingSummary`

## エラーハンドリング

- `UriFormatException` を含む Foundry 設定不正は `FoundryProviderException` に変換
- API レスポンスは `ProblemDetails`（`errorCode` 拡張付き）で返却

## IaC

- `infra/main.bicep`
- `infra/modules/static-web-app.bicep`
- `infra/modules/api-hosting.bicep`
- `infra/parameters/main.parameters.json`

`ClientOrigin` はプレースホルダー固定ではなく、デプロイされた Static Web App の `defaultHostname` から自動設定します。

## Azure へのデプロイ (Bicep)

### 前提条件

| ツール | 確認コマンド | 推奨バージョン |
|--------|-------------|----------------|
| Azure CLI | `az --version` | 2.60 以上 |
| Bicep CLI | `az bicep version` | 最新 (`az bicep upgrade` で更新) |

Bicep CLI が未インストールの場合:

```bash
az bicep install
```

必要な Azure RBAC ロール（デプロイ対象リソースグループに対して）:

- **Contributor** 以上（App Service Plan・App Service・Static Web Apps の作成に必要）

---

### 1. パラメーターファイルの準備

`infra/parameters/main.parameters.json` をコピーして、プレースホルダーを実際の値に置き換えます。  
**このファイルをコミットしないでください。秘密情報が含まれる場合は `.gitignore` に追加してください。**

```jsonc
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "namePrefix": {
      "value": "foundry-billing"          // リソース名のプレフィックス
    },
    "authenticationRequired": {
      "value": true
    },
    "authenticationTenantId": {
      "value": "<Entra テナント ID>"       // 例: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
    },
    "authenticationAudience": {
      "value": "api://<API アプリの App ID>" // 例: api://xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
    },
    "authenticationAuthority": {
      "value": ""                          // 空の場合は authenticationTenantId から自動構成
    },
    "foundryProvider": {
      "value": "Http"                      // IaC デプロイでは Http のみ許可
    },
    "foundryEndpoint": {
      "value": "https://<Foundry エンドポイントの URL>"
    },
    "foundryTenantId": {
      "value": "<Foundry が対象とするテナント ID>"
    },
    "foundrySubscriptionId": {
      "value": "<Foundry が対象とするサブスクリプション ID>"
    },
    "foundryEntraScope": {
      "value": "https://management.azure.com/.default"
    }
  }
}
```

> **注意**  
> - `foundryProvider` は `Http` のみ受け付けます。`Sample` はローカル開発専用で IaC デプロイには使用できません。  
> - `authenticationRequired=true` の場合、`Sample` プロバイダーは起動時に失敗します。  
> - テナント ID・サブスクリプション ID・スコープなどの値を CLI の引数として直接渡すと、シェル履歴に残ります。パラメーターファイル経由での指定を推奨します。

---

### 2. リソースグループの作成

```bash
# bash
az group create \
  --name rg-foundry-billing \
  --location japaneast
```

```powershell
# PowerShell
az group create `
  --name rg-foundry-billing `
  --location japaneast
```

---

### 3. What-If（変更内容の事前確認）

実際のデプロイ前に、作成・変更されるリソースを確認します。

```bash
# bash
az deployment group what-if \
  --resource-group rg-foundry-billing \
  --template-file infra/main.bicep \
  --parameters @infra/parameters/main.parameters.json
```

```powershell
# PowerShell
az deployment group what-if `
  --resource-group rg-foundry-billing `
  --template-file infra/main.bicep `
  --parameters '@infra/parameters/main.parameters.json'
```

---

### 4. デプロイ実行

```bash
# bash
az deployment group create \
  --resource-group rg-foundry-billing \
  --template-file infra/main.bicep \
  --parameters @infra/parameters/main.parameters.json \
  --name foundry-billing-deploy
```

```powershell
# PowerShell
az deployment group create `
  --resource-group rg-foundry-billing `
  --template-file infra/main.bicep `
  --parameters '@infra/parameters/main.parameters.json' `
  --name foundry-billing-deploy
```

デプロイが成功すると、以下の出力が表示されます。

| 出力キー | 説明 |
|----------|------|
| `staticWebAppDefaultHostname` | Static Web Apps のデフォルトホスト名 |
| `apiUrl` | App Service (API) の HTTPS URL |

出力値を確認するコマンド:

```bash
az deployment group show \
  --resource-group rg-foundry-billing \
  --name foundry-billing-deploy \
  --query "properties.outputs" \
  --output table
```

---

### 5. デプロイ後の接続設定

#### API アプリへのコードデプロイ

`infra/main.bicep` はインフラのみを構成します。  
`Dashboard.Api` のアプリコードは別途 App Service へデプロイしてください。

```bash
# 発行プロファイルまたは zip デプロイの例
dotnet publish src/Dashboard.Api -c Release -o ./publish/api
az webapp deploy \
  --resource-group rg-foundry-billing \
  --name foundry-billing-api \
  --src-path ./publish/api \
  --type zip
```

#### Static Web Apps へのクライアントデプロイ

`Dashboard.Client` は Static Web Apps へデプロイします。  
デプロイ方法は [Azure Static Web Apps のドキュメント](https://learn.microsoft.com/azure/static-web-apps/deploy-blazor) を参照してください。

#### クライアントの API URL 設定

`src/Dashboard.Client/wwwroot/appsettings.json` の `Api:BaseUrl` に API URL を設定します。  
本番環境では Static Web Apps と API が同一オリジンになる場合は `/` のままで構いません。

---

### デプロイ後の検証チェックリスト

- [ ] `az deployment group show` で `provisioningState` が `Succeeded` になっている
- [ ] Static Web Apps の URL (`staticWebAppDefaultHostname`) にブラウザでアクセスできる
- [ ] API の URL (`apiUrl`) に対して `GET /api/projects` が正常に応答する
- [ ] API が 401 を返す場合、`authenticationRequired` と Entra アプリ登録の設定を確認する
- [ ] API のアプリ設定（`Foundry__Endpoint` など）が Azure ポータルで正しく設定されている
- [ ] `Foundry__Provider` が `Http` になっており、`Sample` になっていないことを確認する

---

### リソースの削除

> **警告**: 以下のコマンドはリソースグループとその中のすべてのリソースを削除します。

```bash
# bash
az group delete \
  --name rg-foundry-billing \
  --yes \
  --no-wait
```

```powershell
# PowerShell
az group delete `
  --name rg-foundry-billing `
  --yes `
  --no-wait
```
