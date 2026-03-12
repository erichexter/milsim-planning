# Stack Research

**Domain:** Web-based event planning and roster management platform (airsoft/milsim)
**Researched:** 2026-03-12
**Confidence:** HIGH (core stack verified via official docs and Context7-adjacent sources; version numbers from live official sites)

---

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| **Next.js** | 15.x (App Router) | Full-stack React framework | Industry standard for static-frontend + API-backend on a single codebase. App Router gives React Server Components for fast page loads, Route Handlers as REST API endpoints, and built-in TypeScript support. Turbopack dev server now stable. React 19 support. |
| **React** | 19.x | UI rendering | Required by Next.js 15 App Router. Stable as of 2025. |
| **TypeScript** | 5.5+ | Language | Required by Zod 4; standard for all tooling in this stack. |
| **PostgreSQL** | 16.x | Relational database | Best-in-class for relational/hierarchical data (platoon→squad→player). JSON column support for flexible section content. Full-text search built in. AWS RDS / Supabase / Neon all offer managed Postgres. |
| **Drizzle ORM** | 0.38+ (v1 RC) | Database access & migrations | SQL-first TypeScript ORM. Zero-dependency, serverless-ready. Outperforms Prisma at runtime. Native support for `pgTable`, relational queries, and Drizzle Kit migration workflow. Excellent PostgreSQL dialect support. Better choice than Prisma for greenfield projects in 2025+. |
| **better-auth** | latest (1.x) | Authentication & RBAC | Framework-agnostic TypeScript auth library. Has `magicLink` plugin (built-in), `admin` plugin with full RBAC including custom roles + custom permissions, email/password built-in. Self-hosted, users stay in your DB. No per-user billing. Integrates with Drizzle ORM natively. |
| **Tailwind CSS** | 4.x | Utility-first styling | Standard for Next.js projects. Mobile-first responsive design trivial. v4 removes config file boilerplate. |
| **shadcn/ui** | latest | UI component library | Not a package — you own the code. Built on Radix UI primitives (accessible). Tailwind-styled. Sidebar, Data Table, File Input, Drawer all available. Perfect for admin-heavy UIs. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| **Zod** | 4.x | Schema validation | Validate CSV rows, form inputs, API request bodies. Zod 4 is now stable (March 2026 announcement). Use for all input boundaries. |
| **React Hook Form** | 7.x | Form state management | Minimal re-renders, integrates with Zod via `@hookform/resolvers`. Use for all data-entry forms (event creation, player assignment, CSV import review). |
| **@hookform/resolvers** | 3.x | RHF + Zod bridge | Use whenever RHF + Zod together. Required glue. |
| **TanStack Query (React Query)** | 5.x | Server-state management | Cache management for API responses on the client. Use for data-heavy views like roster tables and assignment pages. Next.js Server Components can replace some uses, but RQ still best for client-side optimistic updates and refetching. |
| **Resend** | 4.x SDK | Transactional email | Best developer experience for transactional email in 2025/2026. React Email templates, webhooks, deliverability tooling. Free tier: 3k/month. Use for magic links, event publish notifications, roster change approvals. |
| **react-email** | 3.x | Email template authoring | Companion to Resend. Write email templates in React + Tailwind. Eliminates `<table>` HTML hell. |
| **UploadThing** | 7.x | File uploads (PDF, image, KMZ) | Handles auth on your server, upload bandwidth on theirs. Type-safe file router. Integrates cleanly with Next.js App Router. Better than raw S3 for this scale — no IAM policy complexity. Free tier: 2GB. |
| **Papa Parse** | 5.x | CSV parsing | The standard browser + Node CSV parser. Streams large files, handles malformed rows gracefully, TypeScript types available. Use for importing player rosters. |
| **@next/mdx** or **react-markdown** | latest | Markdown rendering | For faction commander information sections (markdown + attachment display). `react-markdown` with `remark-gfm` is lighter for rendering. Use `@next/mdx` only if you need static page generation from `.mdx` files. |
| **date-fns** | 3.x | Date utilities | Lightweight, tree-shakeable. Use for event date formatting, countdown timers. Do not use moment.js (deprecated). |
| **nuqs** | 2.x | URL search param state | Type-safe URL query string state. Use for filter/sort state in roster tables — survives page refresh and is shareable. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| **Drizzle Kit** | Schema migrations | `drizzle-kit generate` + `drizzle-kit migrate`. Use `push` for dev, generated SQL migrations for production. |
| **ESLint 9** | Linting | Next.js 15 ships ESLint 9 support. Use flat config format. |
| **Prettier** | Code formatting | Standard. Add `prettier-plugin-tailwindcss` to sort class names. |
| **Turbopack** | Dev server | Now stable in Next.js 15. Use `next dev --turbo` for 76% faster startup. |

---

## Installation

```bash
# Core framework
npx create-next-app@latest milsim-platform --typescript --tailwind --app --eslint

# Database & ORM
npm install drizzle-orm postgres
npm install -D drizzle-kit

# Authentication
npm install better-auth

# UI Components (shadcn/ui — installs components individually via CLI)
npx shadcn@latest init

# Validation & Forms
npm install zod react-hook-form @hookform/resolvers

# Server state (client-side)
npm install @tanstack/react-query

# Email
npm install resend react-email @react-email/components

# File uploads
npm install uploadthing @uploadthing/react

# CSV parsing
npm install papaparse
npm install -D @types/papaparse

# Markdown rendering
npm install react-markdown remark-gfm

# Date utilities
npm install date-fns

# URL state
npm install nuqs

# Dev dependencies
npm install -D prettier prettier-plugin-tailwindcss
```

---

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| Next.js 15 | Remix / React Router v7 | Remix has better progressive enhancement story; choose it if you need SSR with form actions without JavaScript. For this app, Next.js App Router is a better fit given static CDN hosting requirement. |
| Next.js 15 | SvelteKit | SvelteKit is excellent but smaller ecosystem, fewer auth/upload libraries. Use if TypeScript + Svelte is a team preference. |
| Drizzle ORM | Prisma | Prisma is more beginner-friendly with better docs. Use Prisma if the team is unfamiliar with SQL or wants a more active query builder DSL. Drizzle is faster at runtime and lighter. |
| better-auth | Auth.js (NextAuth v5) | Auth.js is fine for OAuth-only flows; magic link support is less mature. better-auth has a purpose-built `magicLink` plugin + richer RBAC. |
| better-auth | Lucia Auth | Lucia v3 is now "archived/unmaintained" as of late 2024. Do not use. |
| better-auth | Clerk | Clerk is excellent DX but charges per monthly active user ($0.02+). At 800 users/event × 8 events, cost is predictable but you lose data ownership. Use Clerk if no-op auth setup is the priority. |
| Resend | SendGrid | SendGrid works but has legacy UX, more complex pricing. Resend has better developer experience, React Email integration, simpler free tier. |
| UploadThing | AWS S3 direct | S3 direct requires presigned URLs, CORS config, IAM policies, and custom auth. UploadThing abstracts this. Use S3 direct if you need per-file ACLs, custom CDN, or >250GB storage. |
| Papa Parse | csv-parse (Node) | `csv-parse` is Node.js-only. Papa Parse works in both browser (for client-side preview) and server. Use `csv-parse` if you only process CSVs server-side and prefer streams. |
| TanStack Query | SWR | Both work. TanStack Query has more features (mutations, optimistic updates, devtools). SWR is simpler. For a data-heavy app with roster management, TanStack Query wins. |
| PostgreSQL | SQLite (Turso) | Use SQLite + Turso for truly low-traffic apps or edge deployments. Postgres is more appropriate here given relational complexity and 800 concurrent users. |

---

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| **Lucia Auth** | Archived/unmaintained as of late 2024. Author recommended migrating to better-auth. | `better-auth` |
| **Prisma Client Extensions (v5 new features)** | Still maturing; performance overhead from query engine process. If you're using Drizzle, stay on Drizzle. | `drizzle-orm` |
| **Next.js Pages Router** | Legacy. App Router is production-stable in Next.js 15. Pages Router receives no new features. | App Router |
| **Moment.js** | Deprecated, huge bundle size. | `date-fns` or native `Intl` |
| **Formik** | Higher re-render count than React Hook Form; slower mounting. Less maintained. | `react-hook-form` |
| **Redux / Zustand for server state** | Server state (API data) belongs in TanStack Query, not a client state manager. | `@tanstack/react-query` for server state; `useState`/`useReducer` for local UI state |
| **GraphQL** | Overkill for this app. No mobile SDK requirement, not a real-time system, data shapes are well-known. REST via Next.js Route Handlers is simpler and sufficient. | Next.js Route Handlers (REST) |
| **tRPC** | Excellent for full Next.js monorepos, but adds boilerplate for a data-management app where REST is readable and debuggable. Consider only if you need end-to-end type safety on complex nested mutations. | Standard typed fetch + Zod validation |
| **Socket.io / real-time infra** | Project explicitly out-of-scopes real-time features. | Not needed |
| **next-auth v4 (legacy)** | v4 is superseded by Auth.js v5 (still beta as of early 2026) and better-auth. Avoid starting new projects on v4. | `better-auth` |

---

## Stack Patterns by Variant

**If hosting on Vercel:**
- Use Vercel Postgres (Neon-backed) or Neon directly for zero-config serverless Postgres
- UploadThing integrates natively
- Resend + Vercel env vars for email

**If self-hosting (Railway / Render / Fly.io):**
- Deploy Postgres as a container or managed add-on
- No changes to application code
- Consider `better-auth` standalone server mode if auth needs to be separate service (not required here)

**If budget is a concern:**
- Fly.io free tier + Supabase free tier + UploadThing free tier (2GB) + Resend free tier (3k/mo) = $0 until scale
- Upgrade UploadThing to $10/month (100GB) once PDF/KMZ storage grows

**For the RBAC (5 roles: System Admin, Faction Commander, Platoon Leader, Squad Leader, Player):**
- Use `better-auth` `admin` plugin with custom `createAccessControl()`
- Define 5 roles as constants in `lib/auth/permissions.ts`
- Pass `ac` and `roles` to both server `auth` config and client `createAuthClient`
- Store role in the `user.role` field; better-auth supports comma-separated multi-role strings

---

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| `next@15.x` | `react@19.x`, `react-dom@19.x` | App Router requires React 19. Pages Router can still run React 18. |
| `drizzle-orm@0.38+` | `postgres@3.x`, `pg@8.x` | Use `postgres` (the `postgres.js` driver) not `pg` for new projects — better TypeScript support. |
| `better-auth@1.x` | `drizzle-orm` (native adapter) | Use `drizzleAdapter(db, { provider: "pg" })`. Handles schema migrations via `npx auth migrate`. |
| `zod@4.x` | `@hookform/resolvers@3.x` | Resolver package supports Zod 4 as of v3.9+. Verify resolver version after install. |
| `tailwindcss@4.x` | `shadcn/ui` (current) | shadcn/ui has updated to support Tailwind v4. Use `npx shadcn@latest init` which detects v4 automatically. |
| `@tanstack/react-query@5.x` | Next.js 15 App Router | Use `HydrationBoundary` + `dehydrate` for SSR prefetching. Do NOT use v4 patterns. |
| `uploadthing@7.x` | Next.js 15 App Router | Route Handler–based file router. Use `createUploadthing()` in `app/api/uploadthing/route.ts`. |
| `papaparse@5.x` | Browser + Node | Use `Papa.parse(file, { header: true, skipEmptyLines: true })` for CSV with column headers. |

---

## Sources

- `https://nextjs.org/blog/next-15` — Next.js 15 release notes (stable, Oct 2024); React 19, Turbopack stable, App Router patterns confirmed
- `https://orm.drizzle.team/docs/overview` — Drizzle ORM overview (live, v1 RC active as of March 2026); zero dependencies, serverless-ready confirmed
- `https://better-auth.com/docs/introduction` — better-auth introduction; framework-agnostic, plugin ecosystem confirmed
- `https://better-auth.com/docs/plugins/magic-link` — magic-link plugin docs; built-in, configurable expiry confirmed
- `https://better-auth.com/docs/plugins/admin` — admin plugin docs; custom RBAC with `createAccessControl`, 5+ custom roles confirmed
- `https://resend.com/` — Resend landing page (live, 2026); React Email integration, pricing tiers confirmed
- `https://uploadthing.com/` — UploadThing landing page (live, 2026); auth-on-your-server model, free tier confirmed
- `https://ui.shadcn.com/docs` — shadcn/ui introduction; Tailwind v4 support confirmed
- `https://zod.dev/` — Zod 4 stable release confirmed (live, 2026)
- `https://react-hook-form.com/` — React Hook Form current (v7.x); performance benchmarks confirmed
- Training data (MEDIUM confidence): TanStack Query v5 patterns, date-fns v3, Papa Parse v5, nuqs v2

---

*Stack research for: Airsoft/Milsim Event Planning Platform*
*Researched: 2026-03-12*
