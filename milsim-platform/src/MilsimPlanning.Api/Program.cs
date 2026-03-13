using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
// NOTE: MinimumRoleRequirement handlers are wired in Plan 01-03.
// Policies are registered here because they depend only on AppRoles (defined in this plan).
// Registering now prevents policy-not-found runtime errors when tested in Plan 01-02.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequirePlayer",           p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.Player)));
    options.AddPolicy("RequireSquadLeader",      p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.SquadLeader)));
    options.AddPolicy("RequirePlatoonLeader",    p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.PlatoonLeader)));
    options.AddPolicy("RequireFactionCommander", p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.FactionCommander)));
    options.AddPolicy("RequireSystemAdmin",      p => p.AddRequirements(new MinimumRoleRequirement(AppRoles.SystemAdmin)));
});

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<MagicLinkService>();

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

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// ── Authorization requirement stub ────────────────────────────────────────────
// Full handler (MinimumRoleHandler) is implemented in Plan 01-03.
// This stub allows Program.cs to compile with policy registrations already in place.
public record MinimumRoleRequirement(string MinimumRole) : IAuthorizationRequirement;
