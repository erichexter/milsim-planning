# Phase 1: Foundation - Research

**Researched:** 2026-03-12
**Domain:** ASP.NET Core Identity + EF Core + Npgsql + React/Vite — Authentication & RBAC
**Confidence:** HIGH — all core claims verified against Microsoft official docs (aspnetcore-10.0), Npgsql official docs, and Testcontainers official docs (March 2026)

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| AUTH-01 | User account created via invitation email (imported from CSV) | ASP.NET Core Identity `UserManager.CreateAsync` + token generation; `IEmailSender` abstraction |
| AUTH-02 | Email/password login | `SignInManager.PasswordSignInAsync` — built into Identity |
| AUTH-03 | Magic link (passwordless) login | Custom token table + `UserManager.GenerateUserTokenAsync`/`DataProtectorTokenProvider` approach; single-use enforcement |
| AUTH-04 | Session persists across browser refresh | JWT stored in `localStorage` + Authorization Bearer header; or `HttpOnly` cookie with `SlidingExpiration` |
| AUTH-05 | Password reset via email | `UserManager.GeneratePasswordResetTokenAsync` + `ResetPasswordAsync` — built into Identity |
| AUTH-06 | Logout | `SignInManager.SignOutAsync` (cookies) or client-side token removal (JWT) |
| AUTHZ-01 | 5 roles enforced: System Admin, Faction Commander, Platoon Leader, Squad Leader, Player | ASP.NET Core Identity roles + policy-based authorization with `IAuthorizationRequirement` handlers |
| AUTHZ-02 | Faction Commander full admin access to their event | Policy requiring role `faction_commander` + scope guard checking `event.CreatedByUserId` |
| AUTHZ-03 | Platoon/Squad Leader read-only roster access | Policy requiring minimum role level; scope guard via event membership check |
| AUTHZ-04 | Player can view roster, access info, submit change requests | Policy requiring authenticated user in event roster |
| AUTHZ-05 | Email addresses visible only to leadership (Platoon Leader+) | Service-layer projection: strip email from response DTO when caller role < platoon_leader |
| AUTHZ-06 | Data scoped to user's event membership / IDOR protection | Every query includes event_membership join; service layer `AssertEventScope` pattern |
</phase_requirements>

---

## Summary

ASP.NET Core Identity is the correct foundation for this application's authentication layer. It manages users, password hashing, lockout, email confirmation tokens, and password-reset tokens out-of-the-box and integrates directly with EF Core via `Microsoft.AspNetCore.Identity.EntityFrameworkCore`. For this React SPA + .NET API architecture, **JWT Bearer authentication** is the recommended approach in 2025/2026 for API consumption, with tokens stored in `localStorage` (eliminating cookie-based CSRF concerns). The official ASP.NET Core docs explicitly state this tradeoff: JWT in localStorage removes CSRF risk but requires XSS mitigation; cookie-based auth removes XSS token-theft risk but requires antiforgery.

Magic link authentication does not exist as a built-in Identity feature, but is well-supported through Identity's `DataProtectorTokenProvider` mechanism. A custom token purpose (`"MagicLinkLogin"`) generates signed, time-limited tokens that can be stored in a dedicated `magic_link_tokens` table. The single-use enforcement (mark `used_at`) must be implemented in the service layer.

Authorization is handled via ASP.NET Core's policy-based system, which is richly documented and testable. The recommended pattern for this app is: (1) role hierarchy encoded as a numeric integer map, (2) `IAuthorizationRequirement` + `IAuthorizationHandler` for minimum-role checks, and (3) service-layer scope guards for IDOR protection. IDOR protection is explicitly a service-layer concern — it cannot be enforced at middleware level because the resource ID is not available until the query layer.

**Primary recommendation:** ASP.NET Core Identity + EF Core + Npgsql for the auth data layer; JWT Bearer tokens for API auth; policy-based authorization for RBAC; service-layer scope guards for IDOR (AUTHZ-06 is the most critical requirement and must be architecturally solved in Phase 1 before any feature data is built on top).

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 10.0.x (included in .NET 10 shared framework) | User/role management, password hashing, token generation | Official Microsoft identity system; integrates with EF Core; handles lockout, email confirmation, password reset |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 9.0.x (EF Core 9/10 compatible) | PostgreSQL EF Core provider | Official Npgsql provider; required for PostgreSQL; supports UUIDs, JSONB, enums natively |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.x | EF Core migrations tooling | Required for `dotnet ef migrations add` |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.x | JWT Bearer token validation middleware | Standard for SPA + API auth; included in shared framework |
| `System.IdentityModel.Tokens.Jwt` | 8.x | JWT token creation | Part of Microsoft.IdentityModel; used to issue JWTs after login |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.AspNetCore.DataProtection` | Included in framework | Signing magic link tokens | Use `IDataProtector` with custom purpose string for magic links |
| `FluentValidation.AspNetCore` | 11.x | Server-side request validation | Use for all API endpoint inputs; replaces Data Annotations for complex rules |
| `Serilog.AspNetCore` | 8.x | Structured logging | Set up in Phase 1 so all auth events are logged from day one |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| JWT Bearer | HttpOnly cookie auth | Cookie auth eliminates CSRF concern but requires antiforgery token plumbing for SPA; JWT is simpler for React SPA |
| Custom magic link service | OpenIddict or Duende IdentityServer | Full OAuth2 servers are massively over-engineered for this use case; no SSO requirement |
| EF Core + Npgsql | Dapper | Dapper is faster but loses migration tooling; EF Core migrations are essential for a schema that will evolve across 4 phases |
| Policy-based auth | Role string checks | Role checks scattered in code create the Pitfall 2 anti-pattern; policies are testable and centralized |

### Installation
```bash
# API project
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package System.IdentityModel.Tokens.Jwt
dotnet add package FluentValidation.AspNetCore

# EF tools (global, once)
dotnet tool install --global dotnet-ef
```

---

## Architecture Patterns

### Recommended Solution Structure

```
milsim-planning.sln
├── src/
│   ├── MilsimPlanning.Api/            # ASP.NET Core Web API
│   │   ├── Program.cs                 # Minimal API + DI wiring
│   │   ├── Controllers/
│   │   │   └── Auth/
│   │   │       └── AuthController.cs  # Login, magic-link, logout, reset
│   │   ├── Authorization/
│   │   │   ├── Requirements/
│   │   │   │   └── MinimumRoleRequirement.cs
│   │   │   └── Handlers/
│   │   │       └── MinimumRoleHandler.cs
│   │   ├── Middleware/
│   │   │   └── (reserved — scope errors handled in services)
│   │   └── appsettings.json
│   ├── MilsimPlanning.Application/    # Services, DTOs, interfaces
│   │   ├── Auth/
│   │   │   ├── AuthService.cs
│   │   │   └── MagicLinkService.cs
│   │   └── Common/
│   │       └── ICurrentUser.cs        # Scoped: userId, role, eventMemberships
│   ├── MilsimPlanning.Infrastructure/ # EF Core, Identity, email, external
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs        # Inherits IdentityDbContext<AppUser>
│   │   │   ├── Migrations/
│   │   │   └── Entities/
│   │   │       ├── AppUser.cs         # Extends IdentityUser
│   │   │       ├── UserProfile.cs     # callsign, displayName
│   │   │       ├── Event.cs
│   │   │       ├── EventMembership.cs # user <-> event with role
│   │   │       └── MagicLinkToken.cs
│   │   └── Email/
│   │       └── EmailService.cs
│   └── MilsimPlanning.Domain/         # Enums, domain constants
│       └── Roles.cs                   # Role hierarchy constants
└── tests/
    ├── MilsimPlanning.Api.Tests/      # Integration tests (Testcontainers + xUnit)
    └── MilsimPlanning.Application.Tests/ # Unit tests
```

### Pattern 1: Custom IdentityUser with Separate Profile Table

**What:** Extend `IdentityUser` with minimal extra columns; put player-specific data (callsign, displayName) in a separate `UserProfiles` table.

**When to use:** Always — avoids polluting the Identity schema with domain fields, making future migrations cleaner.

**Why not add columns directly to AspNetUsers:** Identity schema is managed by the framework. Adding non-identity fields to it creates confusion and makes it harder to re-scaffold if needed.

```csharp
// Source: Microsoft Docs — ASP.NET Core Identity (aspnetcore-10.0)
// Infrastructure/Data/Entities/AppUser.cs
public class AppUser : IdentityUser
{
    // Navigation — profile is 1:1, created at same time as user
    public UserProfile Profile { get; set; } = null!;
    public ICollection<EventMembership> EventMemberships { get; set; } = [];
}

// Infrastructure/Data/Entities/UserProfile.cs
public class UserProfile
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;   // FK to AppUser
    public string Callsign { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}

// Infrastructure/Data/AppDbContext.cs
public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventMembership> EventMemberships => Set<EventMembership>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // MUST call base — sets up Identity tables
        builder.Entity<UserProfile>()
            .HasOne(p => p.User)
            .WithOne(u => u.Profile)
            .HasForeignKey<UserProfile>(p => p.UserId);
    }
}
```

**Registration in Program.cs:**
```csharp
// Source: Microsoft Docs — ASP.NET Core Identity (aspnetcore-10.0)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false; // invitation-only flow, no self-registration
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false; // relaxed for player UX
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();  // Registers DataProtectorTokenProvider for password reset + magic links
```

---

### Pattern 2: JWT Bearer Auth for React SPA

**What:** Issue a JWT access token on successful login; React stores it in `localStorage`; every API request sends `Authorization: Bearer <token>`.

**Why JWT over cookies for this app:**
- React SPA on different origin (`:5173` dev / CDN prod) requires CORS cookie configuration which is error-prone
- JWT in localStorage prevents CSRF attacks entirely (tokens are not auto-sent by browser)
- XSS risk is mitigated by ASP.NET Core's default output encoding + no raw HTML rendering on the .NET side
- Simpler to implement and reason about for a small team

**Session persistence (AUTH-04):** JWT stored in `localStorage` persists across browser refresh natively. Set TTL to 7 days for player UX comfort.

```csharp
// Source: Microsoft Docs — JWT Bearer authentication (aspnetcore-10.0)
// Program.cs
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
    };
});

// Application/Auth/AuthService.cs — token generation
private string GenerateJwt(AppUser user, string role)
{
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id),
        new Claim(JwtRegisteredClaimNames.Email, user.Email!),
        new Claim(ClaimTypes.Role, role),
        new Claim("callsign", user.Profile?.Callsign ?? ""),
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
    var token = new JwtSecurityToken(
        issuer: _config["Jwt:Issuer"],
        audience: _config["Jwt:Audience"],
        claims: claims,
        expires: DateTime.UtcNow.AddDays(7),
        signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
    );
    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

**Important note on role in JWT:** Store the role claim but re-validate against DB on sensitive operations (role changes, publish). 7-day JWT means a demoted user retains elevated access for up to 7 days unless token is short-lived or revoked. For this application, role changes are rare admin operations — accept this tradeoff and document it.

---

### Pattern 3: Policy-Based Authorization with Role Hierarchy

**What:** Define a numeric role hierarchy (player=1, squad_leader=2, platoon_leader=3, faction_commander=4, system_admin=5). A single `MinimumRoleRequirement` + handler replaces all scattered `if (role == "x")` checks.

```csharp
// Source: Microsoft Docs — Policy-based authorization (aspnetcore-10.0)

// Domain/Roles.cs
public static class AppRoles
{
    public const string Player = "player";
    public const string SquadLeader = "squad_leader";
    public const string PlatoonLeader = "platoon_leader";
    public const string FactionCommander = "faction_commander";
    public const string SystemAdmin = "system_admin";

    // Numeric hierarchy — higher number = more privilege
    public static readonly Dictionary<string, int> Hierarchy = new()
    {
        [Player] = 1,
        [SquadLeader] = 2,
        [PlatoonLeader] = 3,
        [FactionCommander] = 4,
        [SystemAdmin] = 5
    };
}

// Authorization/Requirements/MinimumRoleRequirement.cs
public record MinimumRoleRequirement(string MinimumRole) : IAuthorizationRequirement;

// Authorization/Handlers/MinimumRoleHandler.cs
public class MinimumRoleHandler : AuthorizationHandler<MinimumRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumRoleRequirement requirement)
    {
        var userRole = context.User.FindFirstValue(ClaimTypes.Role);
        if (userRole is null) return Task.CompletedTask;

        var userLevel = AppRoles.Hierarchy.GetValueOrDefault(userRole, 0);
        var minLevel  = AppRoles.Hierarchy.GetValueOrDefault(requirement.MinimumRole, 99);

        if (userLevel >= minLevel)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

// Program.cs — policy registration
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequirePlayer",            p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.Player)));
    options.AddPolicy("RequireSquadLeader",       p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.SquadLeader)));
    options.AddPolicy("RequirePlatoonLeader",     p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.PlatoonLeader)));
    options.AddPolicy("RequireFactionCommander",  p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.FactionCommander)));
    options.AddPolicy("RequireSystemAdmin",       p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.SystemAdmin)));
});
builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();

// Usage on controller actions:
[Authorize(Policy = "RequireFactionCommander")]
[HttpPost("events")]
public async Task<IActionResult> CreateEvent(...) { ... }
```

---

### Pattern 4: IDOR Protection via Service-Layer Scope Guards (AUTHZ-06)

**What:** Every data-fetch operation in the service layer includes an event membership check. A user cannot access any resource that is not linked to an event they belong to.

**Why service layer, not middleware:** The route parameter (e.g., `eventId`) isn't available until controller execution; resource lookup is needed to scope the check. Middleware runs before controller and doesn't have the resource.

```csharp
// Application/Common/ICurrentUser.cs
public interface ICurrentUser
{
    string UserId { get; }
    string Role { get; }
    IReadOnlySet<Guid> EventMembershipIds { get; }  // pre-loaded from DB on first access
}

// Application/Common/CurrentUser.cs (scoped service)
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContext;
    private readonly AppDbContext _db;
    private IReadOnlySet<Guid>? _cachedEventIds;

    public string UserId =>
        _httpContext.HttpContext!.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    public string Role =>
        _httpContext.HttpContext!.User.FindFirstValue(ClaimTypes.Role)!;

    public IReadOnlySet<Guid> EventMembershipIds =>
        _cachedEventIds ??= LoadEventIds();

    private IReadOnlySet<Guid> LoadEventIds()
    {
        // Cached per-request; single DB query
        return _db.EventMemberships
            .Where(m => m.UserId == UserId)
            .Select(m => m.EventId)
            .ToHashSet();
    }
}

// Application — scope guard helper (used in every service)
public static class ScopeGuard
{
    public static void AssertEventAccess(ICurrentUser currentUser, Guid eventId)
    {
        if (currentUser.Role == AppRoles.SystemAdmin) return; // admins bypass scope
        if (!currentUser.EventMembershipIds.Contains(eventId))
            throw new ForbiddenException($"User does not have access to event {eventId}");
    }
}

// Usage in any service method:
public async Task<RosterDto> GetRosterAsync(Guid eventId)
{
    ScopeGuard.AssertEventAccess(_currentUser, eventId);  // IDOR check FIRST
    // ... DB query
}
```

---

### Pattern 5: Magic Link Authentication (AUTH-03)

**What:** Generate a cryptographically signed, single-use, time-limited token. Send via email. On click, validate token → issue JWT session.

**Implementation approach:** Use ASP.NET Core Identity's `DataProtectorTokenProvider` with a custom purpose. No separate JWT for magic links — use the same `IDataProtector` infrastructure Identity already sets up.

```csharp
// Infrastructure/Data/Entities/MagicLinkToken.cs
public class MagicLinkToken
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public string TokenHash { get; set; } = null!;  // SHA256 of the token
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }            // null = not yet used
}

// Application/Auth/MagicLinkService.cs
public class MagicLinkService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IEmailService _email;

    public async Task SendMagicLinkAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null) return; // don't reveal user existence

        // Generate using Identity's DataProtectorTokenProvider
        var token = await _userManager.GenerateUserTokenAsync(
            user, TokenOptions.DefaultProvider, "MagicLinkLogin");

        // Store hash (not raw token) — defense in depth
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        _db.MagicLinkTokens.Add(new MagicLinkToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)  // 30-minute expiry
        });
        await _db.SaveChangesAsync();

        var link = $"{_config["AppUrl"]}/auth/magic-link/confirm?token={Uri.EscapeDataString(token)}&userId={user.Id}";
        await _email.SendAsync(email, "Your sign-in link", $"Click to sign in: {link}");
    }

    public async Task<string?> VerifyMagicLinkAsync(string userId, string token)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        // Step 1: Verify Identity token signature + expiry
        var valid = await _userManager.VerifyUserTokenAsync(
            user, TokenOptions.DefaultProvider, "MagicLinkLogin", token);
        if (!valid) return null;

        // Step 2: Check our token record (single-use enforcement)
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var record = await _db.MagicLinkTokens
            .Where(t => t.UserId == userId && t.TokenHash == hash && t.UsedAt == null)
            .FirstOrDefaultAsync();
        if (record is null || record.ExpiresAt < DateTime.UtcNow) return null;

        // Step 3: Mark used BEFORE issuing session (atomic)
        record.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return GenerateJwtForUser(user);
    }
}
```

**Important pitfall mitigation:** The confirmation endpoint (`/auth/magic-link/confirm`) must show a "Click to complete login" button — do NOT authenticate on GET. This prevents email security scanners from consuming the token on auto-click.

---

### Pattern 6: CORS for React SPA + .NET API

**What:** Development: React on `:5173`, API on `:5000`. Production: React on CDN, API on separate domain.

```csharp
// Source: Microsoft Docs — CORS in ASP.NET Core (aspnetcore-10.0)
// Program.cs
var devOrigins = "_devOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(devOrigins, policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()); // needed if using cookies in future
});

// Middleware order MATTERS:
app.UseRouting();
app.UseCors(devOrigins);    // BEFORE UseAuthentication
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

**Production pattern:** Move origin list to `appsettings.Production.json`:
```json
{
  "Cors": {
    "AllowedOrigins": ["https://yourdomain.com"]
  }
}
```

**Warning from official docs:** `AllowAnyOrigin` + `AllowCredentials` is an insecure configuration. Always use specific origins.

---

### Anti-Patterns to Avoid

- **Role strings scattered in business logic:** `if (user.Role == "faction_commander")` anywhere except inside `IAuthorizationHandler` — always go through `IAuthorizationService` or `[Authorize(Policy = "...")]`
- **IDOR checks only at controller/middleware:** resource IDs are not available at middleware; all scope guards live in the service layer
- **JWT secret in appsettings.json:** use `dotnet user-secrets` for dev, environment variables / Azure Key Vault for prod
- **Magic link authentication on GET request:** the confirmation link must show a landing page with a button; GET should not complete auth (email scanner protection)
- **Calling `base.OnModelCreating` last in `AppDbContext`:** Identity's `IdentityDbContext.OnModelCreating` must be called first or Identity table configuration is not applied

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Password hashing | Custom bcrypt/argon2 | `UserManager.CreateAsync` (Identity) | Identity uses ASP.NET Core Data Protection; handles salt, cost factor, future algorithm upgrades automatically |
| Email confirmation tokens | Custom UUID token table | `UserManager.GenerateEmailConfirmationTokenAsync` | Identity's token provider handles HMAC signing, expiry, single-use enforcement |
| Password reset tokens | Custom token store | `UserManager.GeneratePasswordResetTokenAsync` + `ResetPasswordAsync` | Handles all edge cases including user existence checks, token expiry |
| Account lockout | Failed-attempt counter | Identity's built-in lockout (`options.Lockout.*`) | Handles concurrent request edge cases |
| Role storage | Custom roles table | `IdentityRole` + `UserManager.AddToRoleAsync` | EF Core-managed via `AspNetUserRoles` junction table |
| DB migrations | Manual SQL scripts | `dotnet ef migrations add` + `dotnet ef database update` | Handles column diffs, index changes, FK cascade rules automatically |

**Key insight:** ASP.NET Core Identity handles 90% of auth complexity. The only custom work needed is (a) magic link tokens, (b) the JWT issuing service, and (c) the role hierarchy/policy handlers.

---

## Common Pitfalls

### Pitfall 1: Calling `AddIdentity` vs `AddDefaultIdentity`

**What goes wrong:** `AddDefaultIdentity` only registers `IdentityUser` roles (not custom roles). `AddIdentity<AppUser, IdentityRole>` is required when using custom roles or custom user types.

**How to avoid:** Use `AddIdentity<AppUser, IdentityRole>()` — not `AddDefaultIdentity`. Then chain `.AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders()`.

**Warning signs:** Roles not appearing in JWT claims; `UserManager.GetRolesAsync` returns empty; runtime error about missing role store.

---

### Pitfall 2: CORS Middleware Order

**What goes wrong:** `UseCors()` called after `UseAuthentication()` causes preflight OPTIONS requests to fail with 401 (the auth middleware rejects them before CORS headers are set).

**How to avoid:** Strict order: `UseRouting()` → `UseCors()` → `UseAuthentication()` → `UseAuthorization()` → `MapControllers()`. This is documented in the official ASP.NET Core CORS docs.

**Warning signs:** React app gets `CORS policy: No 'Access-Control-Allow-Origin' header` on login requests; OPTIONS preflight returns 401.

---

### Pitfall 3: Identity's `OnModelCreating` Not Called in Base Class

**What goes wrong:** `AppDbContext` overrides `OnModelCreating` without calling `base.OnModelCreating(builder)`. Identity's table configuration (`AspNetUsers`, `AspNetRoles`, etc.) is never applied. Migration succeeds but tables are missing.

**How to avoid:** First line of `OnModelCreating` override: `base.OnModelCreating(modelBuilder);`

---

### Pitfall 4: Magic Link Token Consumed by Email Scanner

**What goes wrong:** Corporate email scanner auto-clicks the link URL before the player sees it. Single-use token is consumed. Player cannot log in.

**How to avoid:** Magic link confirmation endpoint (`GET /auth/magic-link/confirm?token=...`) renders a landing page with "Click here to complete sign-in" button. The actual auth action is a POST. Email scanners don't click buttons.

---

### Pitfall 5: JWT Secret Too Short or Predictable

**What goes wrong:** `Jwt:Secret` is a short string (< 256 bits). HMAC-SHA256 tokens can be brute-forced offline if any token is leaked.

**How to avoid:** Generate secret with `openssl rand -base64 32` (produces 256-bit key). Store in `dotnet user-secrets` (dev) or environment variable (prod). Never commit to source control.

---

### Pitfall 6: IDOR Check Missing from One Endpoint

**What goes wrong:** 11 endpoints have scope guards; one is accidentally missed. A player from Event A can access Event B's roster by knowing the event ID.

**How to avoid:** 
1. `ScopeGuard.AssertEventAccess()` is called at the **top** of every service method that accepts an `eventId` parameter.
2. Integration tests explicitly verify cross-event access returns 403 (see Validation Architecture section).
3. Use UUIDs (not sequential integers) for all public-facing IDs — makes guessing IDs impractical.

---

### Pitfall 7: EF Core N+1 Queries in Role/Membership Checks

**What goes wrong:** `ICurrentUser.EventMembershipIds` loaded inside a loop — triggers one DB query per request iteration.

**How to avoid:** `ICurrentUser` is scoped per-request; event membership IDs are loaded once and cached in the `_cachedEventIds` field (see Pattern 4 code above). Single query per request.

---

## Code Examples

### Program.cs Skeleton (Correct Middleware Order)
```csharp
// Source: Microsoft Docs — ASP.NET Core Identity + CORS (aspnetcore-10.0)
var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(/* ... token validation params ... */);

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    // ... policy definitions
});
builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();

// CORS
builder.Services.AddCors(options => { /* ... */ });

// App services
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<MagicLinkService>();

builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("_devOrigins");      // BEFORE auth middleware
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Auth Controller Skeleton (Email/Password Login)
```csharp
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _signInManager.PasswordSignInAsync(
            request.Email, request.Password,
            isPersistent: false, lockoutOnFailure: true);

        if (result.IsLockedOut) return StatusCode(429, "Account locked");
        if (!result.Succeeded) return Unauthorized("Invalid credentials");

        var user = await _userManager.FindByEmailAsync(request.Email);
        var roles = await _userManager.GetRolesAsync(user!);
        var token = _authService.GenerateJwt(user!, roles.FirstOrDefault() ?? AppRoles.Player);

        return Ok(new { token, expiresIn = 604800 }); // 7 days
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        // For JWT: client discards token; server-side is stateless
        // If revocation needed: add to a Redis blocklist (out of scope for Phase 1)
        return Ok();
    }
}
```

### EF Core Migration Commands
```bash
# Create initial migration
dotnet ef migrations add InitialIdentitySchema --project src/MilsimPlanning.Infrastructure --startup-project src/MilsimPlanning.Api

# Apply to database
dotnet ef database update --project src/MilsimPlanning.Infrastructure --startup-project src/MilsimPlanning.Api

# Connection string for local dev (user secrets)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=milsim_dev;Username=postgres;Password=postgres" --project src/MilsimPlanning.Api
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Cookie auth as default for SPAs | JWT Bearer tokens recommended for SPA+API | ~2019–2020 | Eliminates CORS cookie complexity; CSRF non-issue |
| `startup.cs` + `ConfigureServices` | `Program.cs` minimal hosting model | .NET 6 (2021) | Single file; no `Startup` class needed |
| `AddDefaultIdentity` only | `AddIdentity<TUser, TRole>` for custom roles | Stable | Required when using custom user types + roles |
| EF Core InMemory for tests | SQLite in-memory or Testcontainers PostgreSQL | EF Core docs recommend against InMemory | InMemory doesn't enforce FK constraints; use SQLite or Testcontainers |
| `dotnet ef` migrations in same project | Separate Infrastructure project | Best practice, stable | Keeps EF tooling out of API project |

**Deprecated/outdated:**
- `Startup.cs` class: removed in .NET 6+; use `Program.cs` minimal model
- `services.AddMvc()` for pure APIs: use `services.AddControllers()` instead
- `UseEndpoints` with lambda: use `app.MapControllers()` minimal API style

---

## Open Questions

1. **Magic link token storage: DB table vs Redis**
   - What we know: A `magic_link_tokens` table with `used_at` column works correctly for single-use enforcement at Phase 1 scale
   - What's unclear: At higher scale, concurrent redemption race conditions need atomic DB operations (SET used_at WHERE used_at IS NULL)
   - Recommendation: Use DB table with `WHERE used_at IS NULL` conditional update; this is atomic at PostgreSQL isolation levels

2. **JWT revocation for logout**
   - What we know: Stateless JWTs cannot be revoked without a blocklist
   - What's unclear: Whether logout token revocation matters for Phase 1 (role changes are rare; 7-day TTL is acceptable for initial release)
   - Recommendation: Accept stateless logout (client discards token) for Phase 1; add Redis blocklist in Phase 4 if needed

3. **Roles stored in Identity vs JWT claims**
   - What we know: Roles can be stored in `AspNetUserRoles` (DB) and retrieved per-request, OR embedded in JWT (stale risk)
   - What's unclear: Whether role-in-JWT creates a real problem given this app's role-change frequency (very low)
   - Recommendation: Store role in JWT claim; re-fetch from DB only on explicit admin operations (e.g., role reassignment, publish). Acceptable tradeoff for simplicity.

---

## Validation Architecture

> `workflow.nyquist_validation` is `true` in `.planning/config.json` — this section is required.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9+ |
| Config file | `tests/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj` — Wave 0 |
| Quick run command | `dotnet test tests/MilsimPlanning.Api.Tests/ --filter "Category=Unit"` |
| Full suite command | `dotnet test tests/` |

### Test Stack
| Package | Purpose |
|---------|---------|
| `xunit` 2.9+ | Test framework |
| `Microsoft.AspNetCore.Mvc.Testing` | `WebApplicationFactory<Program>` for integration tests |
| `Testcontainers.PostgreSql` 4.x | Real PostgreSQL container per test class |
| `Moq` 4.x | Mocking `IEmailService`, `ICurrentUser` in unit tests |
| `FluentAssertions` 7.x | Readable assertion syntax |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | Notes |
|--------|----------|-----------|-------------------|-------|
| AUTH-01 | Invited user receives welcome email with account-setup link | Integration | `dotnet test --filter "Auth_Invitation"` | Verify `IEmailService.SendAsync` called with correct template; mock email for integration test |
| AUTH-02 | Email/password login returns JWT | Integration | `dotnet test --filter "Auth_Login"` | POST `/api/auth/login`; assert 200 + token present |
| AUTH-02 | Invalid password returns 401 | Integration | `dotnet test --filter "Auth_Login"` | Assert 401 + lockout counter incremented |
| AUTH-02 | Account lockout after 5 failures | Integration | `dotnet test --filter "Auth_Lockout"` | POST 6× invalid; assert 429 on 6th |
| AUTH-03 | Magic link sent to valid email | Integration | `dotnet test --filter "Auth_MagicLink"` | POST `/api/auth/magic-link`; assert email sent |
| AUTH-03 | Magic link token verifies and returns JWT | Integration | `dotnet test --filter "Auth_MagicLink"` | Full round-trip: send link, verify token, get JWT |
| AUTH-03 | Magic link token cannot be used twice | Integration | `dotnet test --filter "Auth_MagicLink_SingleUse"` | Verify → 200; verify same token again → 400/401 |
| AUTH-03 | Expired magic link returns 401 | Integration | `dotnet test --filter "Auth_MagicLink_Expired"` | Insert expired token; verify → 401 |
| AUTH-04 | JWT persists session | Unit | `dotnet test --filter "Auth_JWT_Persistence"` | Parse JWT; assert `exp` claim is 7 days; stateless — no server state to test |
| AUTH-05 | Password reset email sent | Integration | `dotnet test --filter "Auth_PasswordReset"` | POST `/api/auth/password-reset`; assert email |
| AUTH-05 | Password reset token validates and updates password | Integration | `dotnet test --filter "Auth_PasswordReset"` | Full round-trip via `UserManager` |
| AUTH-06 | Logout (client-side) | Unit | `dotnet test --filter "Auth_Logout"` | POST `/api/auth/logout` returns 200; stateless JWT — no server state change |
| AUTHZ-01 | 5 roles enforced — each role gets correct access level | Integration | `dotnet test --filter "Authz_Roles"` | Test each role against each policy: system_admin can all; player blocked from commander endpoints |
| AUTHZ-02 | Faction Commander can access their event | Integration | `dotnet test --filter "Authz_ScopeCommander"` | Create event with commander A; assert commander A → 200, commander B → 403 |
| AUTHZ-03 | Platoon/Squad Leader read-only access | Integration | `dotnet test --filter "Authz_ReadOnlyLeaders"` | Assert GET → 200, POST/PUT → 403 for leader role |
| AUTHZ-04 | Player can read roster | Integration | `dotnet test --filter "Authz_PlayerAccess"` | Assert player in event can GET roster; player not in event → 403 |
| AUTHZ-05 | Email hidden from player/squad leader | Integration | `dotnet test --filter "Authz_EmailVisibility"` | GET `/api/roster` as player; assert `email` field absent from response |
| AUTHZ-05 | Email visible to platoon leader+ | Integration | `dotnet test --filter "Authz_EmailVisibility"` | GET as platoon_leader; assert `email` field present |
| AUTHZ-06 | User in Event A cannot access Event B data | Integration | `dotnet test --filter "Authz_IDOR"` | Create user in event A; attempt GET `/api/events/{eventB.Id}/roster` → 403 |
| AUTHZ-06 | UUID IDs — cannot enumerate by guessing sequential IDs | Design | N/A (code review) | All entities use `Guid` PKs; enforced by DB schema |

### Integration Test Pattern with Testcontainers

```csharp
// Source: Testcontainers official docs (testcontainers.com/guides)
// tests/MilsimPlanning.Api.Tests/AuthTests.cs
public class AuthTests : IAsyncLifetime, IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;
    private HttpClient _client = null!;

    public AuthTests(PostgreSqlFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    // Replace real DB with Testcontainers PostgreSQL
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.AddDbContext<AppDbContext>(opts =>
                        opts.UseNpgsql(_fixture.ConnectionString));
                }));
        _client = factory.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Category", "Auth_Login")]
    public async Task Login_WithValidCredentials_ReturnsJwt()
    {
        // Arrange — seed user via UserManager
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "test@example.com", password = "TestPass1!" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", "Authz_IDOR")]
    public async Task GetRoster_FromDifferentEvent_Returns403()
    {
        // Critical IDOR test — user in EventA cannot read EventB roster
        var eventBId = /* create event B without adding this user */ Guid.NewGuid();
        var token = /* JWT for user only in EventA */;
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await _client.GetAsync($"/api/events/{eventBId}/roster");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// Shared PostgreSQL container (reused across test classes in same run)
public class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
```

### Sampling Rate
- **Per task commit:** `dotnet test tests/MilsimPlanning.Api.Tests/ --filter "Category=Unit"` (fast; no Docker needed)
- **Per wave merge:** `dotnet test tests/` (full suite including Testcontainers integration tests)
- **Phase gate:** Full suite green + IDOR tests pass before `/gsd-verify-work`

### Wave 0 Gaps (Files That Must Exist Before Implementation)
- [ ] `tests/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj` — xUnit + Testcontainers + Mvc.Testing project
- [ ] `tests/MilsimPlanning.Api.Tests/Fixtures/PostgreSqlFixture.cs` — shared container
- [ ] `tests/MilsimPlanning.Api.Tests/Auth/AuthTests.cs` — covers AUTH-01 through AUTH-06
- [ ] `tests/MilsimPlanning.Api.Tests/Authorization/AuthorizationTests.cs` — covers AUTHZ-01 through AUTHZ-06
- [ ] `tests/MilsimPlanning.Application.Tests/MilsimPlanning.Application.Tests.csproj` — unit tests project
- [ ] Framework install: `dotnet add package Testcontainers.PostgreSql` — requires Docker Desktop running

---

## Sources

### Primary (HIGH confidence)
- `learn.microsoft.com/aspnet/core/security/authentication/identity?view=aspnetcore-10.0` — ASP.NET Core Identity setup, UserManager, SignInManager patterns; updated 2026-03-05
- `learn.microsoft.com/aspnet/core/security/authorization/policies?view=aspnetcore-10.0` — Policy-based authorization, IAuthorizationRequirement, handlers; updated 2026-02-17
- `learn.microsoft.com/aspnet/core/security/cors?view=aspnetcore-10.0` — CORS middleware order, AllowCredentials warning, named policies; updated 2026-01-04
- `learn.microsoft.com/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0` — JWT localStorage vs cookie CSRF analysis; updated 2026-01-22
- `learn.microsoft.com/aspnet/core/security/authentication/jwt-authn?view=aspnetcore-10.0` — JWT Bearer setup, `dotnet user-jwts` tooling
- `learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0` — WebApplicationFactory, TestServer, ConfigureTestServices; updated 2026-03-10
- `www.npgsql.org/efcore/` — Npgsql EF Core provider setup, `UseNpgsql`, `AddDbContextPool` pattern
- `testcontainers.com/guides/getting-started-with-testcontainers-for-dotnet/` — PostgreSqlContainer, IAsyncLifetime, xUnit integration
- `learn.microsoft.com/ef/core/get-started/overview/install` — EF Core tooling installation

### Secondary (MEDIUM confidence)
- Existing `.planning/research/ARCHITECTURE.md` — architecture decisions verified compatible with .NET stack
- Existing `.planning/research/PITFALLS.md` — domain pitfalls (IDOR, magic link reuse, role hierarchy) verified aligned with .NET implementation patterns

### Tertiary (LOW confidence — none)
No unverified WebSearch-only claims in this document.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages verified in official NuGet / Microsoft docs
- Architecture: HIGH — patterns sourced from official Microsoft documentation (aspnetcore-10.0 moniker, updated 2025–2026)
- Pitfalls: HIGH — sourced from official docs + cross-referenced with PITFALLS.md domain research
- Test approach: HIGH — sourced from official Testcontainers .NET docs + official ASP.NET Core test docs

**Research date:** 2026-03-12
**Valid until:** 2026-06-12 (stable platform; EF Core and Identity APIs are versioned and backward-compatible; 90-day validity)
