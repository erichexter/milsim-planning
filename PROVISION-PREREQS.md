# Provisioning Prerequisites

Sign up for these services and collect the values before running `provision.ps1`.

## Accounts needed

- **Azure** — https://portal.azure.com
  - Find your Subscription ID: Portal > Subscriptions

- **Neon** (free Postgres) — https://neon.tech
  - Create a project, region: US East
  - Copy the connection string from the dashboard
  - Format: `postgresql://user:pass@ep-xxx.us-east-2.aws.neon.tech/neondb?sslmode=require`

- **Cloudflare R2** (file storage) — https://dash.cloudflare.com
  - R2 > Create bucket
  - R2 > Manage R2 API tokens > Create token (Object Read & Write)
  - Collect: Account ID, Access Key ID, Secret Access Key, Bucket Name

- **Resend** (email) — https://resend.com
  - Add and verify your sending domain
  - API Keys > Create API key
  - Collect: API key, verified from-address

## Values to fill into `provision.ps1`

| Variable | Where to find it |
|---|---|
| `$NeonConnectionString` | Neon project dashboard > Connection string |
| `$JwtSecret` | Generate: `[guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N")` in PowerShell |
| `$R2AccountId` | Cloudflare dashboard > right sidebar |
| `$R2AccessKeyId` | Cloudflare R2 > API token details |
| `$R2SecretAccessKey` | Cloudflare R2 > API token details |
| `$R2BucketName` | Name you gave the R2 bucket |
| `$ResendApiKey` | Resend > API Keys |
| `$ResendFromAddress` | Resend > verified domain sender address |

## CLI tools

- **Azure CLI** — https://aka.ms/installazurecliwindows
  - After install: `az login`

- **GitHub CLI** — https://cli.github.com
  - After install: `gh auth login`

## Run order

1. Sign up for all services above and collect values
2. Fill values into `provision.ps1`
3. `az login` and `gh auth login`
4. `.\provision.ps1`
5. Update `VITE_API_URL` in `.github/workflows/deploy.yml` with the printed Container App URL
6. Push to `master`
