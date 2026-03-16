# RP0 — Milsim Planning Platform

Web-based event planning tool for milsim operations: roster management, faction hierarchy, briefings, and map resources.

## Prerequisites

- Docker Desktop

## Running locally

```bash
docker compose up --build
```

- Web: http://localhost:5173
- API: http://localhost:5000

The `pgdata` volume persists the database between restarts. On first startup the API seeds dev accounts and runs migrations automatically.

### Rebuilding after code changes

```bash
docker compose up --build
```

Or rebuild a single service:

```bash
docker compose build api
docker compose build web
```

### Wiping the database

> **Warning:** This permanently deletes all data including seeded accounts and any imported rosters.

```bash
docker compose down -v
```

## Test accounts

Seeded automatically in Development:

| Email | Password | Role |
|---|---|---|
| `commander@dev.local` | `DevPass123!` | Faction Commander |
| `player@dev.local` | `DevPass123!` | Player |

## Sample import files

Use these with the CSV roster import feature:

| File | Players | Notes |
|---|---|---|
| `sample-roster.csv` | 6 | Minimal roster including both dev accounts |
| `test-data/sample-roster.csv` | 11 | Two-faction roster (BLUFOR / OPFOR) |
| `test-data/sample-roster-200.csv` | 215 | Large roster across 15 teams for stress testing |

## Running tests

```bash
# Backend
dotnet test milsim-platform/src/MilsimPlanning.Api.Tests

# Frontend
pnpm --prefix web test
```
