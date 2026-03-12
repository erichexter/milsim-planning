# Technology Stack

**Project:** Airsoft/MilSim Event Planning Platform
**Researched:** 2026-03-12
**Stack decision:** C# .NET 10 / ASP.NET Core API + React (Vite) SPA + PostgreSQL
**Overall confidence:** HIGH — all versions verified against live NuGet, npm, and official docs as of 2026-03-12

> **Note:** This document replaces a prior recommendation of Next.js 15 + Drizzle ORM + better-auth.
> The user has locked the stack to C# .NET 10 (API) + React/Vite (frontend) + PostgreSQL (database).
> All recommendations below are specific to this architecture.

---

## Recommended Stack at a Glance

```
API:        ASP.NET Core (.NET 10) — Minimal API + Controllers
ORM:        EF Core 10 + Npgsql 10.0.1
Auth:       ASP.NET Core Identity + custom magic-link (hand-rolled with UserManager)
RBAC:       ASP.NET Core Authorization Policies + Role-based middleware
CSV:        CsvHelper 33.x
File Store: Cloudflare R2 via AWS SDK v3 (S3-compatible) + pre-signed URLs
Email:      Resend .NET SDK
Jobs:       .NET BackgroundService + System.Threading.Channels (built-in)
Frontend:   React 19 + Vite 6 + TypeScript
Routing:    React Router v7 (Declarative/SPA mode)
API calls:  TanStack Query (React Query) v5
Forms:      React Hook Form v7 + Zod v3
Components: shadcn/ui (Vite-native)
Markdown:   react-markdown + remark-gfm
Testing:    xUnit + Testcontainers (.NET) / Vitest + Testing Library (React)
```

---

## API Layer (.NET 10 / ASP.NET Core)

### Core Framework

| Technology | NuGet Package | Version | Purpose | Why |
|------------|--------------|---------|---------|-----|
| **ASP.NET Core** | (built-in) | .NET 10.0 | HTTP API host | First-class REST API support, DI container, middleware pipeline. Minimal API for simple endpoints, controllers for complex resource hierarchies. |
| **EF Core** | `Microsoft.EntityFrameworkCore` | 10.0.4 | ORM | Microsoft-maintained, integrates with Identity, supports LINQ queries, migrations out of the box. EF Core 10 is fully released. |
| **Npgsql EF Core** | `Npgsql.EntityFrameworkCore.PostgreSQL` | **10.0.1** | PostgreSQL provider | The official PostgreSQL driver for EF Core. Version 10.0.1 targets .NET 10 directly (released 2026-03-12). Dependency: `Npgsql >= 10.0.2`. |
| **ASP.NET Core Identity** | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 10.0.x | User/role management, password hashing, token generation | Built into the platform. Provides `UserManager<T>`, `SignInManager<T>`, `RoleManager<T>`, token providers. The authoritative answer for auth in .NET — use it, don't fight it. |
| **JWT Bearer** | `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.x | JWT validation for SPA clients | Built-in. Used to validate bearer tokens issued after password login or magic-link verification. The SPA sends `Authorization: Bearer <token>` on all requests. |

### Authentication: The Magic Link Question (Critical)

**Magic link is not built into ASP.NET Core Identity as a first-class feature.** There is no `magic-link` plugin like better-auth has. This is the most non-obvious aspect of the .NET auth story.

**The correct approach: hand-roll it using Identity's built-in token infrastructure.**

ASP.NET Core Identity has a general-purpose token system (`UserManager.GenerateUserTokenAsync` / `UserManager.VerifyUserTokenAsync`) that is designed exactly for this. The email confirmation flow is magic-link under another name, and the same mechanism powers it.

**Implementation pattern (HIGH confidence — official ASP.NET Core Identity docs):**

```csharp
// 1. Generate magic link token (valid 15 minutes)
var token = await _userManager.GenerateUserTokenAsync(
    user,
    tokenProvider: "Default",
    purpose: "magic-link-login"
);
var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
var magicLinkUrl = $"{_baseUrl}/auth/verify-magic-link?userId={user.Id}&token={encodedToken}";

// 2. Send via email (Resend SDK)
await _emailService.SendMagicLinkAsync(user.Email, magicLinkUrl);

// 3. Verify when user clicks link
var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
var valid = await _userManager.VerifyUserTokenAsync(
    user,
    tokenProvider: "Default",
    purpose: "magic-link-login",
    token: decodedToken
);
if (valid) {
    // Issue JWT access token + refresh token
    await _userManager.UpdateSecurityStampAsync(user); // invalidate reuse
    return IssueTokens(user);
}
```

**Why not OpenIddict or Duende IdentityServer?**
- OpenIddict: Full OAuth2/OIDC server — massively over-engineered for a single-tenant SPA. Adds 200+ hours of configuration and debugging for no benefit here.
- Duende IdentityServer: Requires a paid license for production use. Not appropriate for a small internal tool.
- ASP.NET Core Identity + custom magic-link: ~2 days of implementation work, no license cost, stays in your codebase.

**Token storage for magic links:** Store used/issued tokens in a `magic_link_tokens` table (hash the raw token, store expiry, mark as used on redeem). This prevents replay attacks even if the `SecurityStamp` rotation misses edge cases.

### RBAC

ASP.NET Core has first-class role-based authorization. The 5-role hierarchy maps cleanly:

```csharp
// Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CommanderOrAbove", policy =>
        policy.RequireRole("SystemAdmin", "FactionCommander"));
    
    options.AddPolicy("LeaderOrAbove", policy =>
        policy.RequireRole("SystemAdmin", "FactionCommander", "PlatoonLeader", "SquadLeader"));
});

// Controller
[Authorize(Roles = "FactionCommander,SystemAdmin")]
[HttpPost("events/{id}/publish")]
public async Task<IActionResult> PublishEvent(int id) { ... }
```

For event-scoped ownership (user can only edit events in their faction), use a service-layer scope guard — not a middleware attribute:

```csharp
// Services/EventService.cs
public async Task<Event> GetEventForCommanderAsync(int eventId, string userId)
{
    var evt = await _db.Events
        .Include(e => e.Faction)
        .FirstOrDefaultAsync(e => e.Id == eventId);

    if (evt == null) throw new NotFoundException();
    if (evt.Faction.CommanderId != userId && !await IsSystemAdmin(userId))
        throw new ForbiddenException("Event not in your faction");

    return evt;
}
```

### ORM: EF Core 10 + Npgsql

| Package | Version | Notes |
|---------|---------|-------|
| `Microsoft.EntityFrameworkCore` | 10.0.4 | Core ORM |
| `Microsoft.EntityFrameworkCore.Relational` | 10.0.4 | Required for migrations |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | **10.0.1** | PostgreSQL dialect; released 2026-03-12, targets .NET 10 |
| `dotnet-ef` (tool) | 10.0.4 | CLI for migrations: `dotnet ef migrations add` |

**Why EF Core over Dapper?**
- Identity requires EF Core (its `IdentityDbContext` pattern)
- Migration workflow is essential for a team project
- LINQ query syntax reduces SQL errors on hierarchy joins
- Dapper is appropriate as a supplement for complex read queries (e.g., full hierarchy flatten queries), but not as the primary ORM

**Dapper as supplement:** Install `Dapper` (2.1.x) and use it for the one or two complex hierarchy queries that EF generates inefficient SQL for. Example: flattening all platoons→squads→players for an event in a single query.

### CSV Parsing

| Package | Version | Why |
|---------|---------|-----|
| **CsvHelper** | 33.x | The .NET standard for CSV. AWS-sponsored. RFC 4180 compliant. Strongly-typed mapping to C# classes (`GetRecords<T>()`). Handles encoding issues, quoted fields, custom delimiters. 367M total NuGet downloads. |

```csharp
// Usage in RosterService
using var reader = new StringReader(csvContent);
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
var records = csv.GetRecords<RosterImportRow>().ToList();

public class RosterImportRow
{
    [Name("name")] public string Name { get; set; }
    [Name("email")] public string Email { get; set; }
    [Name("callsign")] public string Callsign { get; set; }
    [Name("team")] public string TeamAffiliation { get; set; }
}
```

**Why not System.Formats.Csv (new in .NET 9)?** `System.Formats.Csv` is a low-level reader — no class mapping, no header handling, no error messages. CsvHelper is still the right choice for application-level CSV import with per-row error reporting.

### File Storage: Cloudflare R2 + AWS SDK v3

| Package | Version | Why |
|---------|---------|-----|
| **AWSSDK.S3** | 3.7.x | AWS SDK for .NET S3 client — works with any S3-compatible endpoint including Cloudflare R2, Backblaze B2, MinIO |
| **AWSSDK.Extensions.NETCore.Setup** | 3.7.x | DI registration for ASP.NET Core |

**Why Cloudflare R2 over AWS S3?**
- **No egress fees** — S3 charges per GB downloaded. PDFs and KMZ files are downloaded frequently by players. R2 egress to the internet is free.
- **S3-compatible API** — same `AWSSDK.S3` package, just point at R2's S3-compatible endpoint
- **Pricing**: R2 free tier: 10GB storage / 1M Class A ops / 10M Class B ops per month. This platform will stay in the free tier.
- **Lock-in**: Zero lock-in — R2 uses the S3 API. Switching to AWS S3 is a 5-line config change.

**Configuration:**

```csharp
// Program.cs
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new AmazonS3Client(
        new BasicAWSCredentials(
            config["R2:AccessKeyId"],
            config["R2:SecretAccessKey"]
        ),
        new AmazonS3Config
        {
            ServiceURL = $"https://{config["R2:AccountId"]}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        }
    );
});
```

**Pre-signed URL for upload:**
```csharp
var request = new GetPreSignedUrlRequest
{
    BucketName = _bucket,
    Key = objectKey,
    Verb = HttpVerb.PUT,
    Expires = DateTime.UtcNow.AddMinutes(15),
    ContentType = mimeType
};
var url = _s3.GetPreSignedURL(request);
```

**Pre-signed URL for download (24h TTL for player field use):**
```csharp
var request = new GetPreSignedUrlRequest
{
    BucketName = _bucket,
    Key = objectKey,
    Verb = HttpVerb.GET,
    Expires = DateTime.UtcNow.AddHours(24)
};
var url = _s3.GetPreSignedURL(request);
```

### Transactional Email: Resend

| Package | Version | Why |
|---------|---------|-----|
| **Resend** | latest (1.x) | First-class .NET SDK. DI-friendly (`IResend` interface). Simple API: build `EmailMessage`, call `EmailSendAsync`. Free tier: 3,000 emails/month (sufficient for ~3–4 event notification blasts). |

```bash
dotnet add package Resend
```

```csharp
// Program.cs
builder.Services.AddOptions();
builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(o =>
    o.ApiToken = builder.Configuration["Resend:ApiToken"]!);
builder.Services.AddTransient<IResend, ResendClient>();

// EmailService.cs
var message = new EmailMessage
{
    From = "MilSim Platform <noreply@yourdomain.com>",
    To = { recipientEmail },
    Subject = "Your event assignment is ready",
    HtmlBody = htmlContent
};
await _resend.EmailSendAsync(message);
```

**Why not SendGrid?**
- SendGrid SDK is heavier, pricing is more complex, developer experience lags Resend
- Resend's .NET SDK is purpose-built, maintained by Resend, and integrates with their React Email templates

**Why not MailKit + SMTP?**
- SMTP direct is fine for low volume but has deliverability problems at scale (SPF/DKIM/DMARC setup is fiddly)
- An API-based provider (Resend, SendGrid, Postmark) handles deliverability infrastructure for you
- For 800 recipients, API calls are appropriate; direct SMTP to an external relay has reliability concerns

### Background Jobs: .NET BackgroundService + Channels

**For this scale (~800 recipients), a dedicated job queue library (Hangfire, Quartz.NET) is not needed.**

The built-in `BackgroundService` + `System.Threading.Channels` pattern handles the email blast cleanly:

```csharp
// INotificationQueue.cs
public interface INotificationQueue
{
    ValueTask EnqueueAsync(NotificationJob job, CancellationToken ct = default);
    ValueTask<NotificationJob> DequeueAsync(CancellationToken ct);
}

// NotificationQueue.cs
public class NotificationQueue : INotificationQueue
{
    private readonly Channel<NotificationJob> _channel =
        Channel.CreateBounded<NotificationJob>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public ValueTask EnqueueAsync(NotificationJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);

    public ValueTask<NotificationJob> DequeueAsync(CancellationToken ct)
        => _channel.Reader.ReadAsync(ct);
}

// NotificationWorker.cs (BackgroundService)
public class NotificationWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            await _emailService.SendNotificationAsync(job);
        }
    }
}
```

**When to add Hangfire:**
- If bulk email processing needs to survive an app restart mid-blast (jobs would re-run)
- If you add scheduled jobs (e.g., reminder emails 24h before event)
- Hangfire 1.8.23 (current, 2026-02-05) supports PostgreSQL storage (`Hangfire.PostgreSql` package)

**Hangfire packages if needed:**
```xml
<PackageReference Include="Hangfire.AspNetCore" Version="1.8.23" />
<PackageReference Include="Hangfire.PostgreSql" Version="1.20.x" />
```

### Validation

| Package | Version | Why |
|---------|---------|-----|
| **FluentValidation** | 11.x | .NET standard for complex business rule validation in services. Use for CSV row validation with per-row error collection. |
| **System.ComponentModel.DataAnnotations** | (built-in) | Use for simple `[Required]`, `[MaxLength]` on EF entity properties. |

```bash
dotnet add package FluentValidation.AspNetCore
```

### Testing (.NET)

| Package | Version | Why |
|---------|---------|-----|
| **xUnit** | 2.9.x | Standard .NET test framework. Used by Microsoft itself in ASP.NET Core tests. |
| **Testcontainers.PostgreSql** | 4.x | Spins up a real PostgreSQL instance in Docker for integration tests. Tests run against actual DB, not mocks. Critical for testing EF Core queries and migrations. |
| **Moq** | 4.20.x | Mocking framework for unit tests (mock `IResend`, `IAmazonS3`, etc.) |
| **Microsoft.AspNetCore.Mvc.Testing** | 10.0.x | In-process test server for ASP.NET Core integration tests. Test routes end-to-end without a network. |

```bash
dotnet add package xunit
dotnet add package Testcontainers.PostgreSql
dotnet add package Moq
dotnet add package Microsoft.AspNetCore.Mvc.Testing
```

---

## Frontend (React + Vite)

### Core Framework

| Package | Version | Why |
|---------|---------|-----|
| **React** | 19.x | Current stable. Concurrent rendering, `use()` hook, improved Suspense. |
| **Vite** | 6.x | Fast dev server, Rollup-based production build. First-class React support via `@vitejs/plugin-react`. CDN deploy: `vite build` outputs to `dist/`. |
| **TypeScript** | 5.7+ | Required by shadcn/ui, TanStack Query, and React Router v7 typegen. |

```bash
npm create vite@latest milsim-web -- --template react-ts
```

### Routing: React Router v7 (Declarative/SPA Mode)

| Package | Version | Why |
|---------|---------|-----|
| **react-router** | 7.13.1 (current) | The standard SPA router for React. React Router v7 unifies Remix and React Router under one package. For a pure SPA (no SSR), use **Declarative mode** or **Data mode** with `createBrowserRouter`. |

**Why React Router v7 over TanStack Router v1?**
- React Router has 5× more weekly npm downloads and broader community familiarity
- React Router v7 declarative mode is a drop-in for v6 patterns — minimal learning curve
- TanStack Router's killer feature (type-safe URL params/search params) is valuable but adds setup complexity that isn't warranted for this app's route structure
- Use TanStack Router if the team is starting fresh and wants fully type-safe routing from day one — it is an excellent library

**Why not React Router v7 Framework mode?**
- Framework mode turns React Router into an SSR framework (like Remix). This app uses a separate .NET API. Using framework mode would add SSR complexity for no benefit. Stick to Declarative or Data mode.

```bash
npm install react-router
```

```typescript
// main.tsx
import { createBrowserRouter, RouterProvider } from "react-router";

const router = createBrowserRouter([
  { path: "/", element: <Layout />, children: [
    { index: true, element: <Dashboard /> },
    { path: "events/:eventId", element: <EventDetail /> },
    { path: "events/:eventId/roster", element: <RosterView /> },
    { path: "admin/*", element: <AdminArea /> },
  ]},
  { path: "/auth/*", element: <AuthPages /> },
]);

ReactDOM.createRoot(document.getElementById("root")!).render(
  <RouterProvider router={router} />
);
```

### API Data Fetching: TanStack Query v5

| Package | Version | Why |
|---------|---------|-----|
| **@tanstack/react-query** | 5.x | Server state management: caching, background refetch, loading/error states. Essential for roster tables, hierarchy views, and the event dashboard. |
| **@tanstack/react-query-devtools** | 5.x | Dev-only cache inspector. |

```bash
npm install @tanstack/react-query @tanstack/react-query-devtools
```

```typescript
// Pattern: typed API client + TanStack Query
const { data: roster, isLoading } = useQuery({
  queryKey: ["events", eventId, "roster"],
  queryFn: () => api.get<RosterEntry[]>(`/events/${eventId}/roster`),
});
```

**Auth token management:** Use cookies (HTTP-only, set by the .NET API on login). This is Microsoft's recommended approach for SPAs (per `identity-api-authorization` docs). The API sets `useCookies: true` on the login endpoint. Cookies are automatically sent with fetch requests when `credentials: 'include'` is set. Avoids `localStorage` XSS risk for tokens.

If using bearer tokens instead (e.g., the API issues JWTs): store the access token in React state (not localStorage), refresh via the `/refresh` endpoint when the access token expires. Use an Axios or fetch interceptor.

### Form Handling: React Hook Form + Zod

| Package | Version | Why |
|---------|---------|-----|
| **react-hook-form** | 7.54+ | Minimal re-renders, uncontrolled inputs, excellent TypeScript types. Standard for production React forms. |
| **zod** | 3.24+ | Schema validation library. Use v3 (not v4 — Zod 4 is a new package `zod/v4`; ecosystem resolvers are on v3). Use for validating form schemas and API response shapes. |
| **@hookform/resolvers** | 3.9+ | Bridge between RHF and Zod. Provides `zodResolver`. |

```bash
npm install react-hook-form zod @hookform/resolvers
```

```typescript
const schema = z.object({
  callsign: z.string().min(1).max(32),
  email: z.string().email(),
  teamAffiliation: z.string().optional(),
});

const { register, handleSubmit, formState: { errors } } = useForm<z.infer<typeof schema>>({
  resolver: zodResolver(schema),
});
```

### Component Library: shadcn/ui

| Tool | Why |
|------|-----|
| **shadcn/ui** | Not a package dependency — you own the component code. Built on Radix UI primitives (accessible by default). Tailwind-styled. Has first-class Vite support: `pnpm dlx shadcn@latest init -t vite`. Components include: Button, Table, Dialog, Sheet (drawer), Form, Input, Select, Tabs, Badge, Card, Sidebar, Sonner (toasts), DataTable, FileUpload. |
| **Tailwind CSS** | 4.x | Required by shadcn/ui. `pnpm add tailwindcss@latest` + Vite plugin. |

```bash
pnpm dlx shadcn@latest init -t vite
# Then add components:
pnpm dlx shadcn@latest add button table dialog sheet form input select tabs card
```

**Why not MUI (Material UI)?**
- MUI's theming system and Material Design aesthetic conflict with military/tactical visual language
- MUI components are opaque — customization requires sx prop wrestling or theme overrides
- shadcn/ui components are in your codebase — full control, no versioning fights, no peer dependency issues

**Why not Ant Design?**
- Same theming/ownership issues as MUI
- Enterprise-focused, heavy bundle even with tree-shaking

### Markdown Rendering

| Package | Version | Why |
|---------|---------|-----|
| **react-markdown** | 9.x | Renders markdown strings to React elements. Lightweight. |
| **remark-gfm** | 4.x | GitHub Flavored Markdown plugin — adds tables, strikethrough, task lists. Commanders will use these features in briefing sections. |

```bash
npm install react-markdown remark-gfm
```

```tsx
<ReactMarkdown remarkPlugins={[remarkGfm]}>
  {section.bodyMarkdown}
</ReactMarkdown>
```

### File Upload UI

shadcn/ui does not have a built-in file upload component. Use a simple `<input type="file">` styled with shadcn/ui primitives, or add a drag-drop library:

| Package | Version | Why |
|---------|---------|-----|
| **react-dropzone** | 14.x | Headless drag-and-drop file input. Works with shadcn/ui styling. Mobile-compatible. |

```bash
npm install react-dropzone
```

**Upload flow** (matches the pre-signed URL pattern from ARCHITECTURE.md):
1. User selects file → `react-dropzone` gives `File` object
2. SPA calls `POST /api/files/upload-url` → API returns pre-signed PUT URL + `fileId`
3. SPA `PUT`s the file bytes directly to R2 (not through the API)
4. SPA calls `POST /api/files/{fileId}/confirm` → API marks file as ready
5. Download: SPA calls `GET /api/files/{fileId}/download-url` → API returns 24h pre-signed GET URL

### Testing (React)

| Package | Version | Why |
|---------|---------|-----|
| **Vitest** | 3.x | Vite-native test runner. Same config as Vite, much faster than Jest for Vite projects. |
| **@testing-library/react** | 16.x | DOM-based component testing. Encourages testing behavior, not implementation. |
| **@testing-library/user-event** | 14.x | Simulates real user interactions (typing, clicking, file selection). |
| **happy-dom** | 15.x | Fast DOM implementation for Vitest (alternative to jsdom). |
| **msw** (Mock Service Worker) | 2.x | Intercepts `fetch` calls in tests. Mock API responses without modifying application code. Essential for testing components that call the .NET API. |

```bash
npm install -D vitest @testing-library/react @testing-library/user-event happy-dom msw
```

---

## Solution Structure

The recommended solution structure separates the API and frontend into distinct projects within a monorepo:

```
milsim-platform/                    ← git root
├── milsim-platform.sln             ← .NET solution file
├── src/
│   ├── MilSim.Api/                 ← ASP.NET Core Web API project (.NET 10)
│   │   ├── MilSim.Api.csproj
│   │   ├── Program.cs              ← DI registration, middleware pipeline
│   │   ├── appsettings.json
│   │   ├── Controllers/            ← Controller classes (thin HTTP layer)
│   │   │   ├── EventsController.cs
│   │   │   ├── RosterController.cs
│   │   │   ├── HierarchyController.cs
│   │   │   ├── FilesController.cs
│   │   │   └── AuthController.cs   ← Magic link, login, logout
│   │   ├── Services/               ← Business logic (fat service layer)
│   │   │   ├── EventService.cs
│   │   │   ├── RosterService.cs    ← CSV import pipeline
│   │   │   ├── HierarchyService.cs
│   │   │   ├── FileService.cs      ← Pre-signed URL generation
│   │   │   ├── EmailService.cs     ← Resend wrapper
│   │   │   ├── NotificationService.cs
│   │   │   └── AuthService.cs      ← Magic link token management
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs     ← EF Core DbContext (extends IdentityDbContext)
│   │   │   ├── Entities/           ← EF entity classes
│   │   │   │   ├── ApplicationUser.cs (extends IdentityUser)
│   │   │   │   ├── Event.cs
│   │   │   │   ├── Faction.cs
│   │   │   │   ├── Platoon.cs
│   │   │   │   ├── Squad.cs
│   │   │   │   ├── EventRoster.cs
│   │   │   │   ├── RosterChangeRequest.cs
│   │   │   │   ├── EventSection.cs
│   │   │   │   ├── FileRecord.cs
│   │   │   │   └── MagicLinkToken.cs
│   │   │   └── Migrations/         ← EF Core generated migrations
│   │   ├── Infrastructure/
│   │   │   ├── Storage/
│   │   │   │   └── R2StorageService.cs   ← S3/R2 client wrapper
│   │   │   ├── Email/
│   │   │   │   └── ResendEmailSender.cs  ← IEmailSender implementation
│   │   │   └── BackgroundJobs/
│   │   │       ├── NotificationQueue.cs  ← Channel<T> queue
│   │   │       └── NotificationWorker.cs ← BackgroundService
│   │   ├── Models/                 ← Request/response DTOs
│   │   └── Authorization/          ← Custom auth policies, scope guards
│   │       └── FactionScopeHandler.cs
│   └── MilSim.Tests/               ← xUnit test project
│       ├── MilSim.Tests.csproj
│       ├── Integration/
│       │   ├── RosterServiceTests.cs
│       │   └── EventPublishTests.cs
│       └── Unit/
│           └── CsvImportTests.cs
└── web/                            ← React SPA (Vite)
    ├── package.json
    ├── vite.config.ts
    ├── tsconfig.json
    ├── index.html
    └── src/
        ├── main.tsx                ← Router setup, QueryClient
        ├── lib/
        │   ├── api.ts              ← Typed fetch wrapper (base URL, credentials)
        │   └── auth.ts             ← Auth state (cookie-based, no token storage)
        ├── pages/
        │   ├── auth/               ← Login, magic-link landing
        │   ├── events/             ← Event list, event detail, event editor
        │   ├── roster/             ← Roster view, import, hierarchy builder
        │   ├── files/              ← Document/map upload and download
        │   └── admin/              ← User management (System Admin only)
        ├── components/
        │   ├── ui/                 ← shadcn/ui components (auto-generated)
        │   ├── roster/
        │   ├── hierarchy/
        │   └── files/
        └── hooks/
            ├── useAuth.ts
            ├── useEvent.ts
            └── useRoster.ts
```

**Solution structure rationale:**
- **`milsim-platform.sln`** at root: standard .NET solution file. IDE (Visual Studio, Rider) opens via the solution file.
- **`src/MilSim.Api/`**: Single deployable unit. No microservices at this scale.
- **`src/MilSim.Tests/`**: Separate test project, not in `Api/` — keeps test dependencies out of the production build.
- **`web/`**: Vite project at root level. Not inside `src/` — avoids confusion with .NET `src/` convention. Deploy independently as a static site.
- **`AppDbContext` extends `IdentityDbContext<ApplicationUser>`**: This gives EF Core ownership of both the ASP.NET Core Identity tables (`AspNetUsers`, `AspNetRoles`, etc.) and application tables. Single migration workflow.
- **`Controllers/` (thin) + `Services/` (fat)**: Controllers parse HTTP, call services, return results. Services own all business logic and are independently testable.

---

## Complete Package Manifest

### .NET NuGet Packages (MilSim.Api.csproj)

```xml
<ItemGroup>
  <!-- EF Core + PostgreSQL -->
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.4" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.4" />
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.1" />

  <!-- ASP.NET Core Identity -->
  <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.4" />

  <!-- JWT Bearer Authentication -->
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.4" />

  <!-- CSV Parsing -->
  <PackageReference Include="CsvHelper" Version="33.0.1" />

  <!-- File Storage (S3-compatible for R2) -->
  <PackageReference Include="AWSSDK.S3" Version="3.7.x" />
  <PackageReference Include="AWSSDK.Extensions.NETCore.Setup" Version="3.7.x" />

  <!-- Transactional Email -->
  <PackageReference Include="Resend" Version="1.x" />

  <!-- Validation -->
  <PackageReference Include="FluentValidation.AspNetCore" Version="11.x" />

  <!-- Supplemental query library (optional, for complex reads) -->
  <PackageReference Include="Dapper" Version="2.1.x" />

  <!-- API Documentation -->
  <PackageReference Include="Swashbuckle.AspNetCore" Version="7.x" />
</ItemGroup>

<ItemGroup Label="Tools (dotnet tool install)">
  <!-- Run: dotnet tool install --global dotnet-ef -->
  <!-- Then: dotnet ef migrations add InitialCreate -->
</ItemGroup>
```

### .NET Test Packages (MilSim.Tests.csproj)

```xml
<ItemGroup>
  <PackageReference Include="xunit" Version="2.9.x" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.x" />
  <PackageReference Include="Testcontainers.PostgreSql" Version="4.x" />
  <PackageReference Include="Moq" Version="4.20.x" />
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.4" />
</ItemGroup>
```

### npm Packages (web/package.json)

```json
{
  "dependencies": {
    "react": "^19.0.0",
    "react-dom": "^19.0.0",
    "react-router": "^7.13.1",
    "@tanstack/react-query": "^5.x",
    "react-hook-form": "^7.54.x",
    "zod": "^3.24.x",
    "@hookform/resolvers": "^3.9.x",
    "react-markdown": "^9.x",
    "remark-gfm": "^4.x",
    "react-dropzone": "^14.x"
  },
  "devDependencies": {
    "@vitejs/plugin-react": "^4.x",
    "vite": "^6.x",
    "typescript": "^5.7.x",
    "tailwindcss": "^4.x",
    "@tailwindcss/vite": "^4.x",
    "vitest": "^3.x",
    "@testing-library/react": "^16.x",
    "@testing-library/user-event": "^14.x",
    "happy-dom": "^15.x",
    "msw": "^2.x",
    "@tanstack/react-query-devtools": "^5.x"
  }
}
```

**shadcn/ui is installed separately via CLI (not npm install):**
```bash
pnpm dlx shadcn@latest init -t vite
pnpm dlx shadcn@latest add button table dialog sheet form input select \
  tabs card badge skeleton separator dropdown-menu sonner
```

---

## Alternatives Considered

### Auth Alternatives

| Considered | Recommended | Why Not |
|-----------|-------------|---------|
| **Duende IdentityServer** | ASP.NET Core Identity (custom magic-link) | Production license required ($1,500+/year). Full OAuth2 server is massive overkill for a single-tenant SPA with one auth server. |
| **OpenIddict** | ASP.NET Core Identity (custom magic-link) | Designed for multi-tenant OAuth2/OIDC scenarios. Adds significant configuration complexity for no benefit here. |
| **Auth0 / Okta** | ASP.NET Core Identity (custom magic-link) | Vendor lock-in, per-MAU pricing. At 800 users this might be cheap, but you lose control of user data. |
| **Clerk** | ASP.NET Core Identity (custom magic-link) | Primarily a Next.js/React SDK. .NET support is limited to JWT verification, not first-class. Pricing per MAU. |
| **Cookie-based sessions (no JWT)** | JWT bearer tokens | JWT is stateless — scales horizontally, no session store needed. For a SPA calling a REST API, JWT is the right choice. |

### ORM Alternatives

| Considered | Recommended | Why Not |
|-----------|-------------|---------|
| **Dapper (only)** | EF Core 10 + Npgsql (+ Dapper for complex reads) | Identity requires EF Core. Migration tooling would need a separate tool. For a team project, code-first migrations via `dotnet ef` are essential. |
| **RepoDB** | EF Core 10 + Npgsql | Less mainstream than Dapper; similar tradeoffs. Not worth the ecosystem overhead. |
| **Marten (document DB over Postgres)** | EF Core 10 + Npgsql | Marten treats Postgres as a document store. The hierarchical relational model (events→platoons→squads→players) is a better fit for relational EF Core. |

### Frontend Routing Alternatives

| Considered | Recommended | Why Not |
|-----------|-------------|---------|
| **TanStack Router v1** | React Router v7 (declarative) | Excellent library with better type safety. Choose this if building from scratch and the team values fully type-safe URL params. React Router v7 wins on ecosystem breadth and simpler setup for this app. |
| **Wouter** | React Router v7 (declarative) | Minimalist router. Fine for tiny apps but lacks `useNavigate`, `useSearchParams` maturity. |
| **Next.js with API routes** | React/Vite + .NET API | Stack is locked: user specified React/Vite. Not applicable. |

### Email Alternatives

| Considered | Recommended | Why Not |
|-----------|-------------|---------|
| **SendGrid** | Resend | Heavier SDK, legacy UX, less clean DI integration. Both work. Choose SendGrid only if the team already has a SendGrid account with domain reputation built up. |
| **Postmark** | Resend | Excellent deliverability but priced per email from the start (no meaningful free tier). Use Postmark if Resend free tier is outgrown and deliverability is paramount. |
| **MailKit + SMTP** | Resend | MailKit is the correct .NET SMTP library (not System.Net.Mail). But SMTP for bulk sends requires a relay with your own deliverability management. More work than an API provider. |
| **Azure Communication Services** | Resend | Only worth it if already deep in Azure ecosystem. Resend is cleaner and provider-agnostic. |

### File Storage Alternatives

| Considered | Recommended | Why Not |
|-----------|-------------|---------|
| **AWS S3** | Cloudflare R2 | AWS S3 egress fees are ~$0.09/GB. For a platform where players download PDFs and KMZ map files frequently, egress costs accumulate. R2's S3-compatible API means zero migration cost later if needed. |
| **Azure Blob Storage** | Cloudflare R2 | Works fine but adds Azure dependency. Egress fees similar to S3. R2 free tier is more generous. |
| **UploadThing** | Cloudflare R2 + AWS SDK | UploadThing abstracts auth but is a Next.js/TypeScript-first service. .NET support is thin. Better to use the S3 SDK directly with R2. |
| **Backblaze B2** | Cloudflare R2 | B2 also has no egress fees to CDN. Equally valid choice. R2 preferred because Cloudflare's edge network is faster globally. |

### Background Job Alternatives

| Considered | Recommended | Why Not |
|-----------|-------------|---------|
| **Hangfire** | BackgroundService + Channels | Hangfire adds a dependency and requires a persistent store (Postgres Hangfire storage). For 800 recipients with acceptable synchronous processing, the built-in pattern is sufficient. Add Hangfire if the notification blast must survive restarts or if you add scheduled jobs. |
| **Quartz.NET** | BackgroundService + Channels | Quartz.NET is excellent for scheduled/cron jobs. Not needed for event-triggered email blasts. Add it if you add scheduled reminder emails. |
| **MassTransit** | BackgroundService + Channels | Message bus for distributed systems. Wildly over-engineered for a monolith at this scale. |

---

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| **Duende IdentityServer** | Paid license for production. Overkill for a single-tenant SPA. | ASP.NET Core Identity + custom magic-link |
| **Prisma (C# port / any port)** | No mainstream C# Prisma port exists. The JS Prisma does not run in .NET. | EF Core + Npgsql |
| **NHibernate** | Legacy Java-ported ORM. Active development has declined. EF Core is the modern standard. | EF Core 10 |
| **System.Net.Mail.SmtpClient** | Marked obsolete. Not async-capable on some platforms. | MailKit (if SMTP needed) or Resend (preferred) |
| **log4net / NLog (without Serilog comparison)** | Both work but Serilog is the current community standard in ASP.NET Core with structured logging and sink ecosystem. | Serilog (if custom logging sinks needed), or built-in `ILogger<T>` (sufficient for this app) |
| **Newtonsoft.Json** | Legacy JSON library. System.Text.Json (built-in) is faster and preferred in .NET 6+. | `System.Text.Json` (built-in) |
| **GraphQL (HotChocolate, etc.)** | No real-time requirements, no mobile SDK, well-known data shapes. REST via controllers is simpler and sufficient. | ASP.NET Core controllers (REST) |
| **Redux (React)** | Server state belongs in TanStack Query. Local UI state belongs in `useState`. Redux is for complex shared client state — not present in this app. | TanStack Query for server state; `useState`/`useReducer` for local UI |
| **Formik** | Slower than React Hook Form, less maintained. | React Hook Form v7 |
| **Axios** | Not needed when using TanStack Query with native `fetch`. Adds bundle size. Use native fetch + a thin typed wrapper. | Native `fetch` + typed api.ts wrapper |
| **moment.js** | Deprecated, huge bundle. | `date-fns` or native `Intl` |
| **react-query v4** | Breaking API changes in v5. New projects should start on v5. | `@tanstack/react-query` v5 |

---

## Version Compatibility Notes

| Package | .NET 10 Compatible? | Notes |
|---------|---------------------|-------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | ✅ YES (10.0.1) | Released 2026-03-12, targets `net10.0` directly |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | ✅ YES (10.0.x) | Ships with the .NET 10 SDK |
| `CsvHelper` | ✅ YES (33.x) | Built on .NET Standard 2.0+; runs on any .NET |
| `AWSSDK.S3` | ✅ YES (3.7.x) | Targets `netstandard2.0`; fully compatible |
| `Resend` | ✅ YES | `netstandard2.0`; uses `HttpClient` via DI |
| `Hangfire.PostgreSql` | ✅ YES (if added) | `netstandard2.0`; actively maintained |
| `Testcontainers.PostgreSql` | ✅ YES (4.x) | .NET test tooling, platform-agnostic |
| `react-router` v7 | N/A (frontend) | Works with Vite 6 + React 19 |
| `@tanstack/react-query` v5 | N/A (frontend) | Works with React 19 |
| `shadcn/ui` (Vite init) | N/A (frontend) | First-class Vite support confirmed (official docs) |

---

## Sources

| Source | Confidence | What Was Verified |
|--------|-----------|-------------------|
| [NuGet: Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/10.0.1) | HIGH | .NET 10 compatibility confirmed; released 2026-03-12 |
| [ASP.NET Core Identity for SPAs (MS Docs)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0) | HIGH | `AddIdentityApiEndpoints`, `MapIdentityApi`, cookie vs token modes; supports .NET 10 |
| [ASP.NET Core Identity Introduction (MS Docs)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0) | HIGH | `UserManager.GenerateUserTokenAsync` for custom token purposes (magic link pattern) |
| [ASP.NET Core Background Services (MS Docs)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0) | HIGH | `BackgroundService`, `Channel<T>` queue pattern; supports .NET 10 |
| [CsvHelper official site](https://joshclose.github.io/CsvHelper/) | HIGH | AWS-sponsored, RFC 4180 compliant, .NET Standard |
| [Resend .NET SDK docs](https://resend.com/docs/send-with-dotnet) | HIGH | `dotnet add package Resend`, DI setup, `IResend` interface |
| [EF Core Database Providers (MS Docs)](https://learn.microsoft.com/en-us/ef/core/providers/) | HIGH | Npgsql listed as EF Core 8/9/10 provider (note: docs table lagged; NuGet confirms 10 support) |
| [Hangfire homepage](https://www.hangfire.io/) | HIGH | 1.8.23 current (2026-02-05); PostgreSQL storage available |
| [shadcn/ui Vite installation](https://ui.shadcn.com/docs/installation/vite) | HIGH | `pnpm dlx shadcn@latest init -t vite` confirmed |
| [React Router v7 docs](https://reactrouter.com/home) | HIGH | v7.13.1 current; three modes (Declarative, Data, Framework); declarative = SPA |
| [Npgsql EF Core provider docs](https://www.npgsql.org/efcore/index.html) | MEDIUM | EF 9 config pattern shown; v10 follows same API |
| Training data: `AWSSDK.S3` + R2 compatibility | MEDIUM | Cloudflare R2 is S3-compatible; custom `ServiceURL` + `ForcePathStyle` is the standard pattern. Official Cloudflare R2 .NET page 404'd; pattern is well-documented in community |
| Training data: Zod v3 vs v4 ecosystem state | MEDIUM | Zod v4 is a separate package; `@hookform/resolvers` still primarily supports v3. Using v3 is correct. |
| Training data: TanStack Router v1 | MEDIUM | v1 current; type-safe routing; excellent but React Router v7 wins on ecosystem breadth |

---

*Technology stack research for: Airsoft/MilSim Event Planning Platform*
*API: C# .NET 10 / ASP.NET Core | Frontend: React (Vite) | Database: PostgreSQL*
*Researched: 2026-03-12*
