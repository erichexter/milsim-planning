---
focus: tech
generated: 2026-03-25
---

# Technology Stack

## Summary

Milsim Planning is a full-stack web application with a .NET 10 ASP.NET Core REST API backend and a React 19 / TypeScript frontend. The two services communicate over HTTP via a Vite dev-proxy in development and direct Container App URLs in production. Everything is containerised with Docker Compose.

---

## Languages

**Primary:**
- C# (net10.0) — backend API (`milsim-platform/src/MilsimPlanning.Api/`)
- TypeScript 5.9 — frontend (`web/src/`)

**Secondary:**
- PowerShell — provisioning scripts (`provision.ps1`)

---

## Runtime

**Backend:**
- .NET 10 (ASP.NET Core 10.0)
- Pinned via `global.json`: SDK `10.0.104`, rollForward `latestMinor`
- Docker base image: `mcr.microsoft.com/dotnet/aspnet:10.0`

**Frontend:**
- Node 22 (Slim Docker image: `node:22-slim`)
- Target ES2023 (set in `web/tsconfig.app.json`)

---

## Package Managers

**Frontend:**
- pnpm (installed globally in Docker via `npm install -g pnpm`)
- Lockfile: `web/pnpm-lock.yaml` (present, frozen in CI via `--frozen-lockfile`)

**Backend:**
- NuGet (managed through `.csproj` `<PackageReference>` entries)

---

## Frameworks

**Backend:**
- ASP.NET Core 10.0 — HTTP pipeline, controllers, middleware
- ASP.NET Core Identity 10.0 — user management, password hashing, token providers
- Entity Framework Core 10.0 — ORM
  - Provider: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.1
  - Migrations: auto-applied on startup in all environments (`db.Database.MigrateAsync()`)
- FluentValidation.AspNetCore 11.x — request validation
- Swashbuckle.AspNetCore 7.x — Swagger/OpenAPI (enabled in Development only)
- Microsoft.AspNetCore.Authentication.JwtBearer 10.0 — JWT Bearer authentication
- System.IdentityModel.Tokens.Jwt 8.x — JWT signing/validation

**Frontend:**
- React 19.2 — UI library (`web/src/main.tsx`)
- React Router 7.13 — client-side routing
- TanStack Query (React Query) 5.90 — server-state/data fetching
- TanStack Table 8.21 — headless table logic
- React Hook Form 7.71 + Zod 4.3 — form validation
- Radix UI — accessible headless components (`@radix-ui/react-*`)
- shadcn/ui conventions — component layer built on Radix + CVA (`web/components.json`)
- Tailwind CSS 4.2 — utility-first styling (Vite plugin: `@tailwindcss/vite`)
- class-variance-authority 0.7, clsx 2.1, tailwind-merge 3.5 — class composition
- dnd-kit 6.3/10.0 — drag-and-drop (`@dnd-kit/core`, `@dnd-kit/sortable`, `@dnd-kit/utilities`)
- Sonner 2.0 — toast notifications
- lucide-react 0.577 — icon library
- react-markdown 10.1 + remark-gfm 4.0 — Markdown rendering
- react-dropzone 15.0 — file upload UI
- cmdk 1.1 — command palette component

**Build:**
- Vite 8.0 — frontend bundler and dev server
  - Config: `web/vite.config.ts`
  - Proxy: `/api` → `VITE_API_URL` (default `http://localhost:5000`)
  - Path alias: `@` → `./src`

---

## Testing

**Backend:**
- xUnit 2.9.3 — test runner
- Moq 4.20 — mocking
- FluentAssertions 7.x — assertion DSL
- Microsoft.AspNetCore.Mvc.Testing 10.0 — integration/WebApplicationFactory tests
- Testcontainers.PostgreSql 4.x — real Postgres in containers for integration tests
- coverlet.collector 6.0.4 — code coverage
- Test project: `milsim-platform/src/MilsimPlanning.Api.Tests/`

**Frontend:**
- Vitest 4.1 — test runner (configured in `web/vite.config.ts` under `test:`)
- happy-dom 20.8 — DOM environment
- @testing-library/react 16.3, @testing-library/user-event 14.6, @testing-library/jest-dom 6.9 — React testing utilities
- msw 2.12 — API mocking (Mock Service Worker)
- Setup file: `web/src/test-setup.ts`
- Run command: `pnpm test` (from `web/`)

---

## Linting / Formatting

**Frontend:**
- ESLint 9.39 — configured via `web/eslint.config.js`
  - Plugins: `eslint-plugin-react-hooks`, `eslint-plugin-react-refresh`
  - TypeScript support: `typescript-eslint` 8.56
- TypeScript strict mode enabled (`strict: true`, `noUnusedLocals`, `noUnusedParameters`)
- No Prettier config detected; formatting not enforced by tooling

**Backend:**
- No `.editorconfig` or Roslyn analyzer config detected beyond nullable/implicit usings enabled

---

## Build Configuration

**Frontend:**
- `web/tsconfig.json` — references `tsconfig.app.json` + `tsconfig.node.json`
- `web/tsconfig.app.json` — app code, bundler module resolution, strict, ES2023 target, `@` alias
- `web/vite.config.ts` — Vite + React + Tailwind plugins, proxy, Vitest config

**Backend:**
- `milsim-platform/milsim-platform.slnx` — solution file
- `milsim-platform/src/MilsimPlanning.Api/MilsimPlanning.Api.csproj` — main project
- `milsim-platform/src/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj` — test project

---

## Containerisation

**Docker Compose services:**
| Service | Image / Dockerfile | Port (host→container) |
|---------|-------------------|----------------------|
| `db`    | `postgres:17`     | `5433→5432`          |
| `api`   | `Dockerfile.api`  | `5001→5000`          |
| `web`   | `Dockerfile.web`  | `5173→5173`          |

- `Dockerfile.api` — multi-stage: build on `dotnet/sdk:10.0`, runtime on `dotnet/aspnet:10.0`
- `Dockerfile.web` — single-stage `node:22-slim`, runs `pnpm dev --host 0.0.0.0` (hot-reload in dev)
- Named volumes: `pgdata` (Postgres data), `devuploads` (local file uploads in dev)
- `docker-compose.override.yml` — port overrides for local dev

---

## Platform Requirements

**Development:**
- Docker + Docker Compose (primary dev workflow)
- .NET SDK 10.0.104+ (for local dotnet commands)
- Node 22 + pnpm (for local frontend commands outside Docker)

**Production:**
- ASP.NET Core 10.0 runtime container
- PostgreSQL 17
- Cloudflare R2 (object storage)
- Resend (transactional email)

---

*Stack analysis: 2026-03-25*
