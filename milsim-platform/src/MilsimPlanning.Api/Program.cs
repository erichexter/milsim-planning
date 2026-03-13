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
using MilsimPlanning.Api.Services;
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
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "dev-placeholder-secret-32-chars!!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<MagicLinkService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<RosterService>();
builder.Services.AddScoped<HierarchyService>();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
