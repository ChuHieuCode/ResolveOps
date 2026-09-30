using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using ResolveOps.Api;
using ResolveOps.Api.Infrastructure;
using ResolveOps.Domain.Identity;
using ResolveOps.Modules.Ai;
using ResolveOps.Modules.Claims;
using ResolveOps.Modules.Documents;
using ResolveOps.Modules.Exceptions;
using ResolveOps.Modules.Identity;
using ResolveOps.Modules.Integrations;
using ResolveOps.Modules.Notifications;
using ResolveOps.Modules.Partners;
using ResolveOps.Modules.Reporting;
using ResolveOps.Modules.Shipments;
using ResolveOps.Modules.Tenancy;
using ResolveOps.Modules.Tracking;
using ResolveOps.Modules.Workflow;
using ResolveOps.Persistence;
using ResolveOps.Security;
using ResolveOps.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// ── Service defaults (Serilog, OpenTelemetry, health checks) ─────────────
builder.AddServiceDefaults();

// ── Problem Details (RFC 9457 error envelope) ────────────────────────────
builder.Services.AddProblemDetails();

// ── CORS for Frontend Dev Server ──────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ── OpenAPI ───────────────────────────────────────────────────────────────
builder.Services.AddOpenApi();

// ── Persistence: EF Core via Aspire integration ───────────────────────────
builder.AddSqlServerDbContext<AppDbContext>("resolveops");

// ── Cache: Redis & HybridCache via Aspire integration ────────────────────
builder.AddRedisClient("redis");
builder.AddRedisDistributedCache("redis");
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new Microsoft.Extensions.Caching.Hybrid.HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromHours(12),
        LocalCacheExpiration = TimeSpan.FromHours(1)
    };
});

// ── Identity Core ─────────────────────────────────────────────────────────
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 12; // Spec §25.2 (security constraints)
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>();

// ── Authentication & Authorization (JWT) ──────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SigningKey"] ?? "local-dev-fallback-secret-key-with-enough-bytes-for-sha256";

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
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.RequireActiveTenantMembership, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new TenantMembershipRequirement());
    });
});
builder.Services.AddSingleton<IAuthorizationHandler, TenantMembershipHandler>();

// ── Modules ───────────────────────────────────────────────────────────────
builder.Services.AddIdentityModule();
builder.Services.AddTenancyModule();
builder.Services.AddPartnersModule();
builder.Services.AddShipmentsModule();
builder.Services.AddIntegrationsModule();
builder.Services.AddTrackingModule();
builder.Services.AddExceptionsModule();
builder.Services.AddWorkflowModule();
builder.Services.AddDocumentsModule(builder.Configuration);
builder.Services.AddClaimsModule();
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddReportingModule(builder.Configuration);
builder.Services.AddAiModule(builder.Configuration);

builder.Services.AddScoped<ResolveOps.Application.IErrorMessageProvider, ResolveOps.Persistence.Services.DatabaseErrorMessageProvider>();
builder.Services.AddScoped<ResolveOps.Application.Idempotency.IIdempotencyStore, ResolveOps.Persistence.Services.EfCoreIdempotencyStore>();

// ── Options ──────────────────────────────────────────────────────────────
builder.Services.Configure<ResolveOps.Messaging.Options.OutboxOptions>(
    builder.Configuration.GetSection(ResolveOps.Messaging.Options.OutboxOptions.SectionName));
builder.Services.Configure<ResolveOps.Messaging.Options.RabbitMqConsumerOptions>(
    builder.Configuration.GetSection(ResolveOps.Messaging.Options.RabbitMqConsumerOptions.SectionName));

// ── Health Checks ─────────────────────────────────────────────────────────
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("db", tags: ["ready"]);

// ─────────────────────────────────────────────────────────────────────────

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────
app.UseServiceDefaults();
app.UseMiddleware<CorrelationMiddleware>();
app.UseHttpsRedirection();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// ── OpenAPI (development only) ────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Seed local database
    await DevelopmentSeeder.SeedAsync(app.Services);
}

// ── Endpoints ────────────────────────────────────────────────────────────

app.MapGet("/api/version", () => new
{
    Product = "ResolveOps",
    Phase = "18",
    Description = "AI assistance and RAG knowledge retrieval",
    BuildTimestamp = DateTime.UtcNow.ToString("O"),
})
.WithName("GetVersion")
.WithTags("Meta")
.Produces<object>(StatusCodes.Status200OK);

app.MapIdentityEndpoints();
app.MapTenancyEndpoints();
app.MapPartnersEndpoints();
app.MapShipmentsEndpoints();
app.MapIntegrationsEndpoints();
app.MapTrackingEndpoints();
app.MapExceptionsEndpoints();
app.MapWorkflowEndpoints();
app.MapDocumentsEndpoints();
app.MapClaimsEndpoints();
app.MapNotificationsEndpoints();
app.MapReportingEndpoints();
app.MapAiEndpoints();

// ─────────────────────────────────────────────────────────────────────────

app.Run();

public partial class Program { }
