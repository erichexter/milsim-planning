using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Authorization.Handlers;
using MilsimPlanning.Api.Authorization.Requirements;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Domain;
using MilsimPlanning.Api.Infrastructure.BackgroundJobs;
using MilsimPlanning.Api.Services;
using Resend;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ── Authentication (JWT Bearer) ───────────────────────────────────────────────
// AddIdentity() registers cookie auth as the default scheme; override that so
// [Authorize] attributes use JWT Bearer instead of cookies.
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "dev-placeholder-secret-32-chars!!";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme             = JwtBearerDefaults.AuthenticationScheme;
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});

// ── Authorization ─────────────────────────────────────────────────────────────
// MinimumRoleHandler is the single source of truth for all role hierarchy checks.
// All 5 policies use the same handler — numeric comparison via AppRoles.Hierarchy.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequirePlayer",           p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.Player)));
    options.AddPolicy("RequireSquadLeader",      p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.SquadLeader)));
    options.AddPolicy("RequirePlatoonLeader",    p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.PlatoonLeader)));
    options.AddPolicy("RequireFactionCommander", p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.FactionCommander)));
    options.AddPolicy("RequireSystemAdmin",      p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.SystemAdmin)));
});
builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();

// ── R2 / Cloudflare S3-compatible storage ────────────────────────────────────
// Use LocalFileService when R2 credentials are absent or placeholder values.
// Real Cloudflare account IDs are 32-char hex strings — anything else is a placeholder.
var r2AccountId = builder.Configuration["R2:AccountId"] ?? "";
var useLocalStorage = string.IsNullOrWhiteSpace(r2AccountId)
    || r2AccountId.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
    || r2AccountId.Contains("your-", StringComparison.OrdinalIgnoreCase);

// LocalFileService is always registered so DevUploadController can resolve it.
// In production it is never used for IFileService and no presigned URLs point at it.
builder.Services.AddSingleton<LocalFileService>();

if (useLocalStorage)
{
    // Dev: wire IFileService to local disk implementation
    builder.Services.AddSingleton<IFileService>(sp => sp.GetRequiredService<LocalFileService>());
}
else
{
    // Production: real Cloudflare R2
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
                ForcePathStyle = true  // REQUIRED for R2 — custom domains do NOT work with pre-signed URLs
            }
        );
    });
    builder.Services.AddScoped<IFileService, FileService>();
}

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddOptions();
builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(options =>
{
    options.ApiToken = builder.Configuration["Resend:ApiKey"] ?? "re_placeholder";
});
builder.Services.AddTransient<IResend, ResendClient>();
builder.Services.AddSingleton<INotificationQueue, NotificationQueue>();
builder.Services.AddHostedService<NotificationWorker>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<MagicLinkService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<RosterService>();
builder.Services.AddScoped<HierarchyService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IMapResourceService, MapResourceService>();

// ── Current User (scoped — one instance per HTTP request) ─────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();

// ── MVC + Swagger ─────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration["AppUrl"] ?? "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ── Migrations (all environments) + dev seed ─────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
if (app.Environment.IsDevelopment())
{
    await DevSeedService.SeedAsync(app.Services);
}
else
{
    // Production bootstrap: seed roles and initial admin account on first startup.
    // Skipped automatically once any user exists.
    await ProductionSeedService.SeedAsync(app.Services, app.Configuration);
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Convert ForbiddenException (thrown by ScopeGuard) to HTTP 403
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (ForbiddenException ex)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

app.UseRouting();
app.UseCors();
app.UseStaticFiles();   // serves wwwroot/dev-uploads/ in local dev
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
