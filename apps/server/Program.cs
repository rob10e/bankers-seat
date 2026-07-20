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
    var templatesRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "templates"));
    return new FileTemplateCatalogService(templatesRoot);
});
builder.Services.AddDbContext<BankersSeatDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("BankersSeat");
    options.UseSqlite(connectionString);
});
builder.Services.AddSingleton<ISessionEventBroadcaster, SignalRSessionEventBroadcaster>();
builder.Services.AddScoped<ISessionService, SqliteSessionService>();

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

app.UseCors("WebClient");
app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

app.MapGet("/health", () =>
{
    return Results.Ok(new { status = "ok", timestampUtc = DateTime.UtcNow });
});

app.Run();
