#Requires -Version 5.1
<#
.SYNOPSIS
    Provisions all Azure infrastructure and configures GitHub Actions secrets
    for the RP0 Milsim Planning Platform.

.DESCRIPTION
    Run this script once before the first deployment.
    Re-running is safe — existing resources are skipped or updated.

.NOTES
    Prerequisites:
      - Azure CLI (az) installed and logged in  ->  az login
      - GitHub CLI (gh) installed and logged in ->  gh auth login
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─────────────────────────────────────────────────────────────────────────────
# CONFIGURATION — fill these in before running
# ─────────────────────────────────────────────────────────────────────────────

# Azure
$AzureRegion        = "eastus"
$ResourceGroup      = "rg-milsim"
$AcrName            = "milsimacr"          # globally unique, lowercase, no hyphens
$ContainerAppEnv    = "milsim-env"
$ContainerAppName   = "milsim-api"
$StaticWebAppName   = "milsim-web"

# Secrets — replace ALL placeholder values before running
$NeonConnectionString = "REPLACE_WITH_NEON_CONNECTION_STRING"
$JwtSecret            = "REPLACE_WITH_RANDOM_32_CHAR_STRING"
$R2AccountId          = "REPLACE_WITH_R2_ACCOUNT_ID"
$R2AccessKeyId        = "REPLACE_WITH_R2_ACCESS_KEY_ID"
$R2SecretAccessKey    = "REPLACE_WITH_R2_SECRET_ACCESS_KEY"
$R2BucketName         = "REPLACE_WITH_R2_BUCKET_NAME"
$ResendApiKey         = "REPLACE_WITH_RESEND_API_KEY"
$ResendFromAddress    = "REPLACE_WITH_FROM_EMAIL_ADDRESS"

# GitHub repo (owner/repo format)
$GitHubRepo = "erichexter/milsim-planning"

# ─────────────────────────────────────────────────────────────────────────────
# HELPERS
# ─────────────────────────────────────────────────────────────────────────────

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Success([string]$Message) {
    Write-Host "    OK: $Message" -ForegroundColor Green
}

function Write-Fail([string]$Message) {
    Write-Host ""
    Write-Host "FAILED: $Message" -ForegroundColor Red
    exit 1
}

# ─────────────────────────────────────────────────────────────────────────────
# PREFLIGHT CHECKS
# ─────────────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "RP0 Milsim — Azure Provisioning Script" -ForegroundColor Yellow
Write-Host "=======================================" -ForegroundColor Yellow

Write-Step "Checking prerequisites..."

# az installed
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Fail "Azure CLI not found. Install from https://aka.ms/installazurecliwindows then re-run."
}
Write-Success "Azure CLI found"

# az logged in
$azAccount = az account show 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Fail "Not logged in to Azure. Run: az login"
}
$accountObj = $azAccount | ConvertFrom-Json
Write-Success "Azure logged in as: $($accountObj.user.name)"
Write-Host "    Subscription: $($accountObj.name) ($($accountObj.id))" -ForegroundColor Gray

# Confirm subscription
$confirm = Read-Host "    Continue with this subscription? (y/n)"
if ($confirm -ne "y") {
    Write-Host "Run 'az account set --subscription <id>' to switch, then re-run this script." -ForegroundColor Yellow
    exit 0
}

# gh installed
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Fail "GitHub CLI not found. Install from https://cli.github.com then re-run."
}
Write-Success "GitHub CLI found"

# gh logged in
$ghStatus = gh auth status 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Fail "Not logged in to GitHub CLI. Run: gh auth login"
}
Write-Success "GitHub CLI authenticated"

# gh repo accessible
$repoCheck = gh repo view $GitHubRepo 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Fail "Cannot access GitHub repo '$GitHubRepo'. Check the repo name in this script."
}
Write-Success "GitHub repo accessible: $GitHubRepo"

# Check placeholder values
$placeholders = @(
    @{ Name = "NeonConnectionString"; Value = $NeonConnectionString },
    @{ Name = "JwtSecret";            Value = $JwtSecret },
    @{ Name = "R2AccountId";          Value = $R2AccountId },
    @{ Name = "R2AccessKeyId";        Value = $R2AccessKeyId },
    @{ Name = "R2SecretAccessKey";    Value = $R2SecretAccessKey },
    @{ Name = "R2BucketName";         Value = $R2BucketName },
    @{ Name = "ResendApiKey";         Value = $ResendApiKey },
    @{ Name = "ResendFromAddress";    Value = $ResendFromAddress }
)
$hasPlaceholders = $false
foreach ($p in $placeholders) {
    if ($p.Value -like "REPLACE_*") {
        Write-Host "    NOT SET: $($p.Name)" -ForegroundColor Red
        $hasPlaceholders = $true
    }
}
if ($hasPlaceholders) {
    Write-Fail "Fill in all REPLACE_* values at the top of this script before running."
}
Write-Success "All configuration values set"

# ─────────────────────────────────────────────────────────────────────────────
# 1. RESOURCE GROUP
# ─────────────────────────────────────────────────────────────────────────────

Write-Step "Creating resource group '$ResourceGroup'..."
az group create --name $ResourceGroup --location $AzureRegion | Out-Null
Write-Success "Resource group ready"

# ─────────────────────────────────────────────────────────────────────────────
# 2. AZURE CONTAINER REGISTRY
# ─────────────────────────────────────────────────────────────────────────────

Write-Step "Creating Azure Container Registry '$AcrName'..."
az acr create `
    --resource-group $ResourceGroup `
    --name $AcrName `
    --sku Basic `
    --admin-enabled true | Out-Null
Write-Success "ACR ready"

Write-Step "Retrieving ACR credentials..."
$acrCreds   = az acr credential show --name $AcrName | ConvertFrom-Json
$AcrServer  = "$AcrName.azurecr.io"
$AcrUser    = $acrCreds.username
$AcrPass    = $acrCreds.passwords[0].value
Write-Success "ACR credentials retrieved"

# ─────────────────────────────────────────────────────────────────────────────
# 3. CONTAINER APPS ENVIRONMENT
# ─────────────────────────────────────────────────────────────────────────────

Write-Step "Creating Container Apps environment '$ContainerAppEnv'..."
az containerapp env create `
    --name $ContainerAppEnv `
    --resource-group $ResourceGroup `
    --location $AzureRegion | Out-Null
Write-Success "Container Apps environment ready"

# ─────────────────────────────────────────────────────────────────────────────
# 4. AZURE CONTAINER APP
# ─────────────────────────────────────────────────────────────────────────────

Write-Step "Creating Container App '$ContainerAppName'..."

# Use a placeholder image for initial creation — CI/CD will replace it on first push
az containerapp create `
    --name $ContainerAppName `
    --resource-group $ResourceGroup `
    --environment $ContainerAppEnv `
    --image "mcr.microsoft.com/dotnet/aspnet:10.0" `
    --target-port 5000 `
    --ingress external `
    --min-replicas 0 `
    --max-replicas 3 `
    --env-vars `
        "ASPNETCORE_ENVIRONMENT=Production" `
        "ASPNETCORE_URLS=http://+:5000" `
        "Jwt__Issuer=milsim-platform" `
        "Jwt__Audience=milsim-platform" `
        "Jwt__Secret=$JwtSecret" `
        "ConnectionStrings__DefaultConnection=$NeonConnectionString" `
        "AppUrl=PENDING_STATIC_WEB_APP_URL" `
        "R2__AccountId=$R2AccountId" `
        "R2__AccessKeyId=$R2AccessKeyId" `
        "R2__SecretAccessKey=$R2SecretAccessKey" `
        "R2__BucketName=$R2BucketName" `
        "Resend__ApiKey=$ResendApiKey" `
        "Resend__FromAddress=$ResendFromAddress" | Out-Null

Write-Success "Container App created"

Write-Step "Getting Container App URL..."
$ContainerAppFqdn = az containerapp show `
    --name $ContainerAppName `
    --resource-group $ResourceGroup `
    --query "properties.configuration.ingress.fqdn" `
    --output tsv
Write-Success "Container App URL: https://$ContainerAppFqdn"

# ─────────────────────────────────────────────────────────────────────────────
# 5. AZURE STATIC WEB APPS
# ─────────────────────────────────────────────────────────────────────────────

Write-Step "Creating Azure Static Web App '$StaticWebAppName'..."
Write-Host "    Note: this will open a browser window to authorize GitHub access." -ForegroundColor Gray

az staticwebapp create `
    --name $StaticWebAppName `
    --resource-group $ResourceGroup `
    --location "eastus2" `
    --sku Free `
    --source "https://github.com/$GitHubRepo" `
    --branch master `
    --app-location "/web" `
    --output-location "dist" `
    --login-with-github | Out-Null

Write-Success "Static Web App created"

Write-Step "Getting Static Web App URL and deployment token..."
$StaticWebAppHostname = az staticwebapp show `
    --name $StaticWebAppName `
    --resource-group $ResourceGroup `
    --query "defaultHostname" `
    --output tsv
$StaticWebAppToken = az staticwebapp secrets list `
    --name $StaticWebAppName `
    --resource-group $ResourceGroup `
    --query "properties.apiKey" `
    --output tsv
Write-Success "Static Web App URL: https://$StaticWebAppHostname"

# ─────────────────────────────────────────────────────────────────────────────
# 6. UPDATE CONTAINER APP WITH STATIC WEB APP URL (for CORS AppUrl)
# ─────────────────────────────────────────────────────────────────────────────

Write-Step "Updating Container App AppUrl with Static Web App URL..."
az containerapp update `
    --name $ContainerAppName `
    --resource-group $ResourceGroup `
    --set-env-vars "AppUrl=https://$StaticWebAppHostname" | Out-Null
Write-Success "AppUrl updated"

# ─────────────────────────────────────────────────────────────────────────────
# 7. SERVICE PRINCIPAL FOR GITHUB ACTIONS
# ─────────────────────────────────────────────────────────────────────────────

Write-Step "Creating service principal for GitHub Actions..."
$SubscriptionId = $accountObj.id
$AzureCredentials = az ad sp create-for-rbac `
    --name "milsim-deploy" `
    --role contributor `
    --scopes "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup" `
    --sdk-auth
Write-Success "Service principal created"

# ─────────────────────────────────────────────────────────────────────────────
# 8. SET GITHUB ACTIONS SECRETS
# ─────────────────────────────────────────────────────────────────────────────

Write-Step "Setting GitHub Actions secrets..."

gh secret set AZURE_CREDENTIALS     --repo $GitHubRepo --body $AzureCredentials
Write-Success "AZURE_CREDENTIALS set"

gh secret set ACR_LOGIN_SERVER      --repo $GitHubRepo --body $AcrServer
Write-Success "ACR_LOGIN_SERVER set"

gh secret set ACR_USERNAME          --repo $GitHubRepo --body $AcrUser
Write-Success "ACR_USERNAME set"

gh secret set ACR_PASSWORD          --repo $GitHubRepo --body $AcrPass
Write-Success "ACR_PASSWORD set"

gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN --repo $GitHubRepo --body $StaticWebAppToken
Write-Success "AZURE_STATIC_WEB_APPS_API_TOKEN set"

# ─────────────────────────────────────────────────────────────────────────────
# DONE — print next steps
# ─────────────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "=======================================" -ForegroundColor Green
Write-Host "  Provisioning complete!" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Container App URL : https://$ContainerAppFqdn" -ForegroundColor Yellow
Write-Host "  Static Web App URL: https://$StaticWebAppHostname" -ForegroundColor Yellow
Write-Host ""
Write-Host "  One manual step remaining:" -ForegroundColor Cyan
Write-Host "  Update VITE_API_URL in .github/workflows/deploy.yml:" -ForegroundColor Cyan
Write-Host ""
Write-Host "    VITE_API_URL: https://$ContainerAppFqdn" -ForegroundColor White
Write-Host ""
Write-Host "  Then push to master to trigger the first deployment." -ForegroundColor Cyan
Write-Host ""
