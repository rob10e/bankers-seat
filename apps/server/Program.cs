using BankersSeat.Server.Application.Diagnostics;
using BankersSeat.Server.Application.Health;
using BankersSeat.Server.Application.Sessions;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Application.Authentication;
using BankersSeat.Server.Application.Audit;
using BankersSeat.Server.Application.RoomSecurity;
using BankersSeat.Server.Application.Retention;
using BankersSeat.Server.Infrastructure.Persistence;
using BankersSeat.Server.Realtime;
using AspNetCoreRateLimit;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddRouting(options => options.LowercaseUrls = true);

builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddMemoryCache();
builder.Services.AddInMemoryRateLimiting();

var jwtKey = builder.Configuration["Jwt:SigningKey"] ?? Guid.NewGuid().ToString();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT authentication failed: {Message}", context.Exception?.Message);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClient", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddSingleton<ITemplateCatalogService>(serviceProvider =>
{
    var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
    var configuredTemplatesRoot = builder.Configuration["Templates:Root"];
    var templatesRoot = string.IsNullOrWhiteSpace(configuredTemplatesRoot)
        ? Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "templates"))
        : Path.GetFullPath(configuredTemplatesRoot);
    var fileService = new FileTemplateCatalogService(templatesRoot);
    return new CachedTemplateCatalogService(fileService);
});

var databaseProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";

builder.Services.AddDbContext<BankersSeatDbContext>(options =>
{
    var connectionString = databaseProvider == "Postgres"
        ? builder.Configuration.GetConnectionString("BankersSeatPostgres")
        : builder.Configuration.GetConnectionString("BankersSeat");

    if (databaseProvider == "Postgres")
    {
        options.UseNpgsql(connectionString, o =>
        {
            o.EnableRetryOnFailure(3);
            o.MinBatchSize(2);
            o.MaxBatchSize(100);
        });
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

builder.Services.AddSingleton<ISessionEventBroadcaster, SignalRSessionEventBroadcaster>();
builder.Services.AddScoped<ISessionService, SqliteSessionService>();
builder.Services.AddScoped<IHealthService, DefaultHealthService>();
builder.Services.AddScoped<IDiagnosticsService, DiagnosticsService>();

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IRoomSecurityService, RoomSecurityService>();
builder.Services.AddScoped<ISessionMetadataService, SessionMetadataService>();
builder.Services.AddScoped<IDataRetentionService, DataRetentionService>();

// Phase 5 — Template Ecosystem
builder.Services.AddScoped<ITemplatePackageService, TemplatePackageService>();
builder.Services.AddSingleton<ITemplateDraftService, TemplateDraftService>();

var app = builder.Build();

app.UseIpRateLimiting();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BankersSeatDbContext>();
    dbContext.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

var webRootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(webRootPath))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseCors("WebClient");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<GameHub>("/hubs/game");
if (Directory.Exists(webRootPath))
{
    app.MapFallbackToFile("index.html");
}


app.Run();
