using BankersSeat.Server.Application.Diagnostics;
using BankersSeat.Server.Application.Health;
using BankersSeat.Server.Application.Sessions;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Infrastructure.Persistence;
using BankersSeat.Server.Realtime;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddRouting(options => options.LowercaseUrls = true);
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
builder.Services.AddDbContext<BankersSeatDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("BankersSeat");
    options.UseSqlite(connectionString);
});
builder.Services.AddSingleton<ISessionEventBroadcaster, SignalRSessionEventBroadcaster>();
builder.Services.AddScoped<ISessionService, SqliteSessionService>();
builder.Services.AddScoped<IHealthService, DefaultHealthService>();
builder.Services.AddScoped<IDiagnosticsService, DiagnosticsService>();

var app = builder.Build();

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
app.MapControllers();
app.MapHub<GameHub>("/hubs/game");
if (Directory.Exists(webRootPath))
{
    app.MapFallbackToFile("index.html");
}


app.Run();
