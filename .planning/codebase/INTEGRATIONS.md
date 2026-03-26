---
focus: tech
generated: 2026-03-25
---

# External Integrations

## Summary

The platform integrates with three external services: PostgreSQL (primary database), Cloudflare R2 (object storage for attachments and map files), and Resend (transactional email for magic links and notification blasts). All integration credentials are supplied via environment variables; in development, placeholder values trigger local fallback implementations.

---

## Data Storage

### Primary Database

- **Type:** PostgreSQL 17
- **ORM:** Entity Framework Core 10.0 via `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.1
- **DbContext:** `milsim-platform/src/MilsimPlanning.Api/Data/AppDbContext.cs`
- **Migrations:** `milsim-platform/src/MilsimPlanning.Api/Data/Migrations/` — auto-applied on every startup via `db.Database.MigrateAsync()` in `Program.cs`
- **Connection env var:** `ConnectionStrings__DefaultConnection`
  - Dev default: `Host=db;Port=5432;Database=milsim_dev;Username=postgres;Password=postgres`
  - Local default (non-Docker): `Host=localhost;Port=5432;Database=milsim_dev;...` (from `appsettings.json`)
- **Identity tables:** Managed by ASP.NET Core Identity (`AspNetUsers`, `AspNetRoles`, etc.) via `AddIdentity<AppUser, IdentityRole>()`
- **Test database:** `Testcontainers.PostgreSql` spins up a real Postgres container per test run (no SQLite in-memory substitute)

### File Storage — Cloudflare R2

- **Service:** Cloudflare R2 (S3-compatible object storage)
- **SDK:** `AWSSDK.S3` 4.0.19 + `AWSSDK.Extensions.NETCore.Setup` 4.0.3
- **Client config:** `AmazonS3Client` pointed at `https://{R2:AccountId}.r2.cloudflarestorage.com` with `ForcePathStyle = true`
- **Service abstraction:** `IFileService` (`milsim-platform/src/MilsimPlanning.Api/Services/FileService.cs`)
- **Dev fallback:** When `R2:AccountId` is absent or contains "placeholder"/"your-", `LocalFileService` is wired as `IFileService` instead. Files are served from the `devuploads` Docker volume via ASP.NET Core static files middleware.
- **Upload flow:** Presigned PUT URLs are generated server-side and returned to the frontend; the browser uploads directly to R2 (or local endpoint in dev). After upload, the client calls a "confirm" endpoint to persist metadata.
- **Required env vars:**
  - `R2__AccountId` — 32-character hex Cloudflare account ID
  - `R2__AccessKeyId` — R2 API token access key
  - `R2__SecretAccessKey` — R2 API token secret
  - `R2__BucketName` — target bucket name (e.g. `milsim-dev`)

---

## Email

### Resend

- **Service:** [Resend](https://resend.com) — transactional email
- **SDK:** `Resend` 0.2.2 NuGet package (`IResend`, `ResendClient`)
- **Service wrapper:** `milsim-platform/src/MilsimPlanning.Api/Services/EmailService.cs` implements `IEmailService`
- **Use cases:**
  - Magic link / passwordless login emails (`MagicLinkService.cs`)
  - Notification blast emails sent to all event registrants (`NotificationWorker` background job)
- **Background processing:** `NotificationWorker` is a hosted service (`IHostedService`) consuming from `INotificationQueue` — email sends are queued and processed asynchronously, not inline with the HTTP request
- **Required env var:** `Resend__ApiKey` (prefixed `re_` for real keys; placeholder `re_placeholder` disables sending in dev without crashing)
- **Frontend origin used for link generation:** `AppUrl` env var (e.g. `http://localhost:5173` in dev)

---

## Authentication & Identity

- **Provider:** Custom — ASP.NET Core Identity + JWT Bearer (no third-party auth provider)
- **Strategy:** Magic link / passwordless login (no passwords stored for standard users)
  - `MagicLinkService.cs` generates and validates short-lived tokens
  - On validation, a JWT is issued and returned to the client
- **JWT config:**
  - Issuer/Audience: `milsim-platform` (configurable via `Jwt__Issuer`, `Jwt__Audience`)
  - Signing key env var: `Jwt__Secret` (minimum 32 chars; dev placeholder used if absent)
  - Algorithm: HMAC-SHA symmetric (`SymmetricSecurityKey`)
- **Token storage (frontend):** JWT stored client-side; accessed via `web/src/lib/auth.ts` (`getToken()`, `clearToken()`)
- **Authorization model:** Role hierarchy with 5 levels — `Player`, `SquadLeader`, `PlatoonLeader`, `FactionCommander`, `SystemAdmin`
  - Implemented via custom `MinimumRoleRequirement` + `MinimumRoleHandler` (`milsim-platform/src/MilsimPlanning.Api/Authorization/`)
  - 5 named policies: `RequirePlayer`, `RequireSquadLeader`, `RequirePlatoonLeader`, `RequireFactionCommander`, `RequireSystemAdmin`

---

## CSV Processing

- **Library:** `CsvHelper` 33.x — used for roster import validation and commit
- **Flow:** Commander uploads a CSV; `/roster/validate` parses and returns row-level errors; `/roster/commit` persists valid rows as `EventPlayer` records
- **Sample file:** `sample-roster.csv` at repo root

---

## Frontend API Communication

- **Mechanism:** Native `fetch` wrapped in a typed client at `web/src/lib/api.ts`
- **No external HTTP client library** (no Axios or similar)
- **Dev routing:** Vite proxy in `web/vite.config.ts` rewrites `/api/*` to the backend (avoids CORS in dev)
- **Production routing:** `VITE_API_URL` env var set at build time to backend Container App URL; prepended to all fetch calls
- **Data fetching:** TanStack Query (`@tanstack/react-query`) wraps all `api.*` calls in components/hooks for caching, background refetch, and loading/error states

---

## Monitoring & Observability

- **Error tracking:** None detected (no Sentry, Datadog, Application Insights)
- **Logging:** ASP.NET Core built-in logging
  - Dev: `Information` level for default, `Warning` for `Microsoft.AspNetCore`
  - Production: `Warning` for all (configured in `appsettings.Production.json`)
- **Swagger / OpenAPI:** Swashbuckle 7.x — enabled in Development environment only (accessible at `/swagger`)

---

## CI/CD & Deployment

- **Containerisation:** Docker Compose (see STACK.md for service details)
- **CI pipeline:** Not detected in repository
- **Deployment target:** Not explicitly defined in repo; environment variable names (`AppUrl`, Container App URL pattern) suggest Azure Container Apps
- **Provisioning scripts:** `provision.ps1`, `PROVISION-PREREQS.md`, `DEPLOYMENT.md` — PowerShell-based provisioning

---

## Environment Configuration Summary

All configuration is injected via environment variables (Docker Compose `environment:` or platform secrets). No `.env` files are committed.

| Variable | Service | Purpose |
|----------|---------|---------|
| `ConnectionStrings__DefaultConnection` | API | PostgreSQL connection string |
| `Jwt__Issuer` | API | JWT issuer claim |
| `Jwt__Audience` | API | JWT audience claim |
| `Jwt__Secret` | API | JWT HMAC signing key (≥32 chars) |
| `AppUrl` | API | Frontend origin for CORS + magic link URLs |
| `Resend__ApiKey` | API | Resend transactional email API key |
| `R2__AccountId` | API | Cloudflare account ID (32-char hex) |
| `R2__AccessKeyId` | API | Cloudflare R2 access key |
| `R2__SecretAccessKey` | API | Cloudflare R2 secret key |
| `R2__BucketName` | API | R2 bucket name |
| `VITE_API_URL` | Web | Backend base URL for production builds |
| `ASPNETCORE_ENVIRONMENT` | API | `Development` or `Production` |

---

## Webhooks & Callbacks

- **Incoming:** None detected
- **Outgoing:** None detected (Resend sends email synchronously via SDK; no webhook callbacks registered)

---

*Integration audit: 2026-03-25*
