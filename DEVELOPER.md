# DEVELOPER.md — milsim-planning Quick Reference

_Operational guide for building, testing, and verifying the milsim-planning
application. All commands verified from actual agent runs._

---

## Build Commands

### Backend (.NET)
```bash
cd /home/ub24/milsim-planning/milsim-platform
dotnet build src/MilsimPlanning.Api/MilsimPlanning.Api.csproj        # API only
dotnet build src/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj  # Tests
```

### Frontend (React/Vite)
```bash
cd /home/ub24/milsim-planning/web
npm install    # Required after git clean or fresh clone
npm run build  # Production build
```

---

## Test Commands

### Backend Tests (108 baseline)
```bash
cd /home/ub24/milsim-planning/milsim-platform
dotnet test src/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj
```
- **Framework:** xUnit + FluentAssertions + Testcontainers (PostgreSQL)
- **Baseline:** 108 passing tests (as of pre-feature state)
- **Note:** Always rebuild before testing. Do NOT use `--no-build` after code
  changes — stale binaries cause false `PendingModelChangesWarning` failures.

### Frontend Tests (62 baseline)
```bash
# PREFERRED — run inside Docker container (always works)
docker exec milsim-planning-web-1 sh -c "cd /app && npx vitest run"

# ALTERNATIVE — run locally (only if node_modules exists)
cd /home/ub24/milsim-planning/web
npm install   # if node_modules is missing
npx vitest --run
```
- **Framework:** Vitest + @testing-library/react + MSW
- **Baseline:** 62 passing tests across 16 test files
- **IMPORTANT:** Do NOT try `pnpm test` (pnpm not installed), `npm test -- --run`
  (vitest not on PATH without npx), or bare `npx vitest run` without
  `node_modules`. Use the Docker container method to avoid wasted retries.

---

## Docker Compose

### Container Names and Ports
| Container | Service | Port | Notes |
|-----------|---------|------|-------|
| `milsim-planning-api-1` | .NET API | 5000 | Backend API |
| `milsim-planning-db-1` | PostgreSQL 17 | 5432 | Database (healthy) |
| `milsim-planning-web-1` | Vite dev server | 5173 | Frontend |

### Commands
```bash
cd /home/ub24/milsim-planning

# Start all containers
docker compose up -d

# Check status
docker compose ps

# Rebuild API after code changes
docker compose build api && docker compose up -d api

# View API logs
docker compose logs api --tail=20

# Health check — confirm API is responding
curl -s http://localhost:5000/api/events | head -3
```

---

## Database Access

### Connection Details
| Field | Value |
|-------|-------|
| Container | `milsim-planning-db-1` |
| User | `postgres` |
| Database | `milsim_dev` |
| Port | 5432 |

**IMPORTANT:** The DB user is `postgres`, NOT `milsim`. The database name is
`milsim_dev`, NOT `milsim`.

### Common Queries
```bash
# Interactive psql
docker exec -it milsim-planning-db-1 psql -U postgres -d milsim_dev

# Check applied migrations
docker exec milsim-planning-db-1 psql -U postgres -d milsim_dev \
  -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'

# List test users
docker exec milsim-planning-db-1 psql -U postgres -d milsim_dev \
  -c 'SELECT "Email", "PasswordHash" IS NOT NULL as has_pw FROM "AspNetUsers";'

# Check user roles and event memberships
docker exec milsim-planning-db-1 psql -U postgres -d milsim_dev \
  -c 'SELECT u."Email", em."EventId", em."Role" FROM "AspNetUsers" u JOIN "EventMemberships" em ON u."Id" = em."UserId";'
```

---

## Authentication

### Auth Method
The API uses **magic link** authentication with an optional **password login**
for dev-seeded accounts.

### Test Accounts (from DevSeedService)
| Email | Password | Role | Event ID |
|-------|----------|------|----------|
| `commander@dev.local` | `DevPass123!` | `faction_commander` | `019d40d1-8144-7e44-81f6-8e78223e8f99` |
| `player@dev.local` | `DevPass123!` | `player` | `019d40d1-8144-7e44-81f6-8e78223e8f99` |

### Get a JWT Token
```bash
# Password login — MUST use --data-raw (not -d) to handle the ! character
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  --data-raw '{"email":"commander@dev.local","password":"DevPass123!"}' \
  -H "Content-Type: application/json" | jq -r '.token')

echo $TOKEN  # Should be ~399 chars
```

**IMPORTANT:** Use `curl --data-raw` instead of `curl -d` for the login
endpoint. The `!` in `DevPass123!` causes JSON parse errors with `-d` due to
shell escaping.

### Magic Link Flow (alternative)
```bash
# 1. Request magic link
curl -s -X POST http://localhost:5000/api/auth/magic-link \
  -H "Content-Type: application/json" \
  -d '{"email":"commander@dev.local"}'

# 2. Token appears in API logs (dev mode prints to stdout)
docker compose logs api 2>&1 | grep -i "magic\|token\|link" | tail -3

# 3. Or query the DB directly
docker exec milsim-planning-db-1 psql -U postgres -d milsim_dev \
  -c 'SELECT "TokenHash", "ExpiresAt" FROM "MagicLinkTokens" ORDER BY "ExpiresAt" DESC LIMIT 1;'
```

### Using a JWT
```bash
# Example: GET all events
curl -s http://localhost:5000/api/events \
  -H "Authorization: Bearer $TOKEN"

# Example: GET frequencies for an event
curl -s http://localhost:5000/api/events/019d40d1-8144-7e44-81f6-8e78223e8f99/frequencies \
  -H "Authorization: Bearer $TOKEN"
```

---

## EF Core Migrations

### How Migrations Work
- Migrations auto-apply on API startup (see `Program.cs`)
- Migration files live in `Data/Migrations/`
- Each migration has 3 files: `.cs` (SQL), `.Designer.cs` (snapshot), and the
  shared `AppDbContextModelSnapshot.cs`

### Creating a New Migration
```bash
cd /home/ub24/milsim-planning/milsim-platform
dotnet ef migrations add <MigrationName> \
  --project src/MilsimPlanning.Api/MilsimPlanning.Api.csproj
```

### Column Type Conventions
- `string?` properties map to PostgreSQL `text` type (NOT `varchar(50)`)
- Do NOT add `HasMaxLength()` or `HasColumnType("character varying(50)")` to
  the snapshot/designer for nullable string columns — this causes
  `PendingModelChangesWarning` test failures
- Always verify: after creating migration files, run `dotnet test` (with
  rebuild) to confirm no snapshot mismatch

### Troubleshooting: "Column already exists"
If the API crashes on startup with `column "X" already exists`, a prior
migration attempt partially applied. Fix:
```bash
# Register the migration as already applied
docker exec milsim-planning-db-1 psql -U postgres -d milsim_dev \
  -c "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('<MigrationId>', '10.0.5') ON CONFLICT DO NOTHING;"

# Restart the API
docker compose up -d api
```

---

## Git Workflow

### Branch Naming
```
feature/HEX-<N>-short-description
```

### Commit and PR
```bash
cd /home/ub24/milsim-planning
git checkout -b feature/HEX-X-short-description
git add <specific files>
git commit -m "[HEX-X] feat: description"
git push -u origin feature/HEX-X-short-description
gh pr create --title "[HEX-X] feat: description" --body "Closes HEX-X." --base master
```

**Do NOT merge to master.** Human merges after Code Reviewer approval and QA
sign-off.
