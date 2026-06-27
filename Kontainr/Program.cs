using Kontainr.Components;
using Kontainr.Data;
using Kontainr.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dataDir = builder.Configuration["KONTAINR_DATA"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataDir);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")));

// SQLite metrics database
builder.Services.AddDbContextFactory<MetricsDbContext>(options =>
    options.UseSqlite($"Data Source={Path.Combine(dataDir, "kontainr-metrics.db")}"));

// Health checks (liveness + metrics DB readiness)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<MetricsDbContext>("metrics-db");

builder.Services.AddSingleton<SshSettingsService>();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddSingleton<AuditService>();
builder.Services.AddSingleton<WebhookService>();
builder.Services.AddSingleton<StatsHistoryService>();

// Docker multi-host management
builder.Services.AddSingleton<DockerHostManager>();
builder.Services.AddSingleton<DockerServiceFactory>();
// Keep DockerService as singleton pointing at local for backward compatibility
builder.Services.AddSingleton<DockerService>(sp =>
    sp.GetRequiredService<DockerServiceFactory>().GetLocalService());

builder.Services.AddSingleton<SshSessionManager>();
builder.Services.AddSingleton<ContainerEventService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ContainerEventService>());
builder.Services.AddSingleton<ContainerFileService>();
builder.Services.AddSingleton<RegistryService>();
builder.Services.AddSingleton<ScheduledRestartService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ScheduledRestartService>());
builder.Services.AddSingleton<LogAlertService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LogAlertService>());

// Metrics services
builder.Services.AddSingleton<MetricsQueryService>();
builder.Services.AddSingleton<MetricsCollectionService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MetricsCollectionService>());

var app = builder.Build();

// Apply EF Core migrations to the metrics database.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MetricsDbContext>>().CreateDbContext();

    // Transition support: databases created by earlier versions used EnsureCreated()
    // and therefore have no __EFMigrationsHistory. Migrate() would then try to re-create
    // existing tables and fail. If we find such a legacy database, baseline it by marking
    // all current migrations as already applied (its schema already matches them).
    var creator = db.Database.GetService<IRelationalDatabaseCreator>();
    if (creator.Exists() && !db.Database.GetAppliedMigrations().Any())
    {
        var historyRepo = db.GetService<IHistoryRepository>();
        var efVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "10.0.0";
        db.Database.ExecuteSqlRaw(historyRepo.GetCreateIfNotExistsScript());
        foreach (var migrationId in db.Database.GetMigrations())
            db.Database.ExecuteSqlRaw(historyRepo.GetInsertScript(new HistoryRow(migrationId, efVersion)));
    }

    db.Database.Migrate();
}

app.UseMiddleware<BasicAuthMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
