# Deployment Plan — RP0 Milsim Planning Platform

## Target Stack

| Layer | Service | Cost |
|---|---|---|
| API | Azure Container Apps (scale-to-zero, consumption plan) | ~$1–3/mo |
| Database | Neon free tier (serverless Postgres) | Free |
| Frontend | Azure Static Web Apps free tier | Free |
| File storage | Cloudflare R2 (already configured) | ~$0 |
| Email | Resend (already configured) | Free tier |
| CI/CD | GitHub Actions on push to `master` | Free |

**Estimated total: ~$1–3/mo**

Azure Static Web Apps is used for the frontend because its free tier includes TLS, a CDN,
and GitHub Actions integration out of the box — no extra certificate or proxy setup needed.

---

## Prerequisites (one-time, before running the pipeline)

- Azure CLI (`az`) installed and logged in
- Azure subscription ready
- Neon account created at https://neon.tech (free)
- Cloudflare R2 bucket and credentials already in place
- Resend API key already in place

---

## One-Time Azure Resource Provisioning

Run these `az` CLI commands once to set up the Azure infrastructure.
Replace placeholder values (ALL_CAPS) with your own.

```bash
# 1. Resource group
az group create --name rg-milsim --location eastus

# 2. Azure Container Registry (stores API Docker images)
az acr create \
  --resource-group rg-milsim \
  --name milsimacr \
  --sku Basic \
  --admin-enabled true

# 3. Get ACR credentials (save these — needed for GitHub secrets)
az acr credential show --name milsimacr

# 4. Azure Container Apps environment
az containerapp env create \
  --name milsim-env \
  --resource-group rg-milsim \
  --location eastus

# 5. Azure Container App (initial deploy — image will be updated by CI/CD)
az containerapp create \
  --name milsim-api \
  --resource-group rg-milsim \
  --environment milsim-env \
  --image mcr.microsoft.com/dotnet/aspnet:10.0 \
  --target-port 5000 \
  --ingress external \
  --min-replicas 0 \
  --max-replicas 3 \
  --env-vars \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5000 \
    Jwt__Issuer=milsim-platform \
    Jwt__Audience=milsim-platform \
    "Jwt__Secret=REPLACE_WITH_RANDOM_32_CHAR_STRING" \
    "ConnectionStrings__DefaultConnection=REPLACE_WITH_NEON_CONNECTION_STRING" \
    "AppUrl=REPLACE_WITH_STATIC_WEB_APP_URL" \
    "R2__AccountId=REPLACE" \
    "R2__AccessKeyId=REPLACE" \
    "R2__SecretAccessKey=REPLACE" \
    "R2__BucketName=REPLACE" \
    "Resend__ApiKey=REPLACE" \
    "Resend__FromAddress=REPLACE"

# 6. Azure Static Web Apps (frontend)
az staticwebapp create \
  --name milsim-web \
  --resource-group rg-milsim \
  --location eastus2 \
  --sku Free \
  --source https://github.com/erichexter/milsim-planning \
  --branch master \
  --app-location "/web" \
  --output-location "dist" \
  --login-with-github

# 7. Get Static Web Apps deployment token (save for GitHub secrets)
az staticwebapp secrets list --name milsim-web --resource-group rg-milsim
```

After provisioning, note the Container App URL:

```bash
az containerapp show \
  --name milsim-api \
  --resource-group rg-milsim \
  --query properties.configuration.ingress.fqdn \
  --output tsv
```

Then update the `AppUrl` env var on the Container App to the Static Web App URL,
and update `VITE_API_URL` in the GitHub Actions workflow (see below) to the Container App URL.

---

## GitHub Actions Secrets to Configure

Go to your GitHub repo Settings > Secrets and variables > Actions and add:

| Secret | Value |
|---|---|
| `AZURE_CREDENTIALS` | Service principal JSON (see below) |
| `ACR_LOGIN_SERVER` | e.g. `milsimacr.azurecr.io` |
| `ACR_USERNAME` | From `az acr credential show` |
| `ACR_PASSWORD` | From `az acr credential show` |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | From `az staticwebapp secrets list` |

To generate the `AZURE_CREDENTIALS` service principal:

```bash
az ad sp create-for-rbac \
  --name milsim-deploy \
  --role contributor \
  --scopes /subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/rg-milsim \
  --sdk-auth
```

Paste the full JSON output as the `AZURE_CREDENTIALS` secret.

---

## Code Changes Required

### 1. `Dockerfile.api`
Remove the hardcoded `ASPNETCORE_ENVIRONMENT=Development` line.
The Container App environment variable controls this in production.

**Change:** Delete this line:
```
ENV ASPNETCORE_ENVIRONMENT=Development
```

### 2. `Program.cs`
Move `db.Database.MigrateAsync()` outside the `IsDevelopment()` guard so migrations
run on startup in all environments. Dev seed stays dev-only.

**Change:** Replace the startup block:
```csharp
// Before
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DevSeedService.SeedAsync(app.Services);
}

// After
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
if (app.Environment.IsDevelopment())
{
    await DevSeedService.SeedAsync(app.Services);
}
```

### 3. `appsettings.Production.json` (new file)
Create at `milsim-platform/src/MilsimPlanning.Api/appsettings.Production.json`:

```json
{
  "AllowedHosts": "*",
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

All secrets (connection string, JWT secret, R2 keys, Resend key) are injected as
environment variables on the Container App — never committed to the repo.

### 4. `.github/workflows/deploy.yml` (new file)
See the CI/CD pipeline section below.

---

## CI/CD Pipeline

Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy

on:
  push:
    branches: [master]

jobs:
  test-backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Run backend tests
        run: dotnet test milsim-platform/src/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj

  test-frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: pnpm/action-setup@v4
        with:
          version: latest
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'pnpm'
          cache-dependency-path: web/pnpm-lock.yaml
      - name: Install dependencies
        run: pnpm --prefix web install --frozen-lockfile
      - name: Run frontend tests
        run: pnpm --prefix web test --run

  deploy-api:
    needs: [test-backend, test-frontend]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Log in to Azure Container Registry
        uses: docker/login-action@v3
        with:
          registry: ${{ secrets.ACR_LOGIN_SERVER }}
          username: ${{ secrets.ACR_USERNAME }}
          password: ${{ secrets.ACR_PASSWORD }}

      - name: Build and push API image
        uses: docker/build-push-action@v5
        with:
          context: .
          file: Dockerfile.api
          push: true
          tags: ${{ secrets.ACR_LOGIN_SERVER }}/milsim-api:${{ github.sha }},${{ secrets.ACR_LOGIN_SERVER }}/milsim-api:latest

      - name: Log in to Azure
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Deploy to Azure Container Apps
        run: |
          az containerapp update \
            --name milsim-api \
            --resource-group rg-milsim \
            --image ${{ secrets.ACR_LOGIN_SERVER }}/milsim-api:${{ github.sha }}

  deploy-frontend:
    needs: [test-backend, test-frontend]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: pnpm/action-setup@v4
        with:
          version: latest

      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'pnpm'
          cache-dependency-path: web/pnpm-lock.yaml

      - name: Install dependencies
        run: pnpm --prefix web install --frozen-lockfile

      - name: Build frontend
        run: pnpm --prefix web build
        env:
          VITE_API_URL: https://REPLACE_WITH_CONTAINER_APP_FQDN

      - name: Deploy to Azure Static Web Apps
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: upload
          app_location: web
          output_location: dist
          skip_app_build: true
```

---

## Neon Database Setup

1. Create a free account at https://neon.tech
2. Create a new project — region: US East (closest to Azure `eastus`)
3. Copy the connection string — it looks like:
   `postgresql://user:password@ep-xxx.us-east-2.aws.neon.tech/neondb?sslmode=require`
4. Set this as `ConnectionStrings__DefaultConnection` on the Azure Container App

Neon auto-suspends after 5 minutes of inactivity on the free tier.
The first request after idle will be ~1 second slower while the DB wakes up.
This is acceptable for a low-traffic prototype.

---

## Post-Deploy Checklist

- [ ] Container App URL noted and set as `AppUrl` env var
- [ ] Static Web App URL noted and `VITE_API_URL` updated in workflow
- [ ] All GitHub Actions secrets configured
- [ ] Push to `master` — verify pipeline passes
- [ ] Hit the Container App URL `/swagger` to confirm API is up
- [ ] Hit the Static Web App URL to confirm frontend loads
- [ ] Log in with a real account to confirm auth works end-to-end
- [ ] Upload a test file to confirm R2 integration works
- [ ] Send a test notification to confirm Resend integration works

---

## Environment Variables Reference

Full list of environment variables required on the Azure Container App:

| Variable | Description |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://+:5000` |
| `ConnectionStrings__DefaultConnection` | Neon PostgreSQL connection string |
| `Jwt__Issuer` | `milsim-platform` |
| `Jwt__Audience` | `milsim-platform` |
| `Jwt__Secret` | Random 32+ character string |
| `AppUrl` | Azure Static Web Apps URL (for CORS) |
| `R2__AccountId` | Cloudflare R2 account ID |
| `R2__AccessKeyId` | Cloudflare R2 access key |
| `R2__SecretAccessKey` | Cloudflare R2 secret key |
| `R2__BucketName` | Cloudflare R2 bucket name |
| `Resend__ApiKey` | Resend API key |
| `Resend__FromAddress` | Sender email address |
