using Kontainr.Components;
using Kontainr.Data;
using Kontainr.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Resolve authentication before anything else so a misconfigured instance fails at
// startup rather than coming up wide open. Kontainr drives the Docker socket, so an
// unauthenticated instance is equivalent to remote root on this host.
//
// The EF Core tooling builds this same host to read the DbContext, and it has no reason
// to carry credentials, so design-time invocations are exempt — they never serve traffic.
var isEfDesignTime = AppDomain.CurrentDomain.FriendlyName == "ef";
var authOptions = isEfDesignTime
    ? AuthOptions.Disabled()
    : AuthOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(authOptions);

var dataDir = builder.Configuration["KONTAINR_DATA"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "data");
SecureDirectory.Create(dataDir);

// The key ring can live on a separate volume from the data it protects, so that a
// backup or a shared mount of the data directory does not also hand over the keys.
var keysDir = builder.Configuration["KONTAINR_KEYS"] ?? Path.Combine(dataDir, "keys");
SecureDirectory.Create(keysDir);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir));

// Encrypt the key ring itself when a passphrase is supplied. Without it the keys are
// stored in the clear and are only as protected as the filesystem they sit on.
var keyPassphrase = builder.Configuration["KONTAINR_KEY_PASSPHRASE"];
if (!string.IsNullOrWhiteSpace(keyPassphrase))
{
    builder.Services.AddSingleton(new KeyPassphrase(keyPassphrase));
    builder.Services.AddSingleton<PassphraseXmlEncryptor>();
    builder.Services.AddOptions<KeyManagementOptions>()
        .Configure<PassphraseXmlEncryptor>((options, encryptor) => options.XmlEncryptor = encryptor);
}

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

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Kontainr.Startup");

if (!authOptions.Enabled && !isEfDesignTime)
{
    startupLogger.LogWarning(
        "Authentication is DISABLED (Auth__Disabled=true). Anyone who can reach this instance has full " +
        "control of the Docker host, including a shell inside any container. Only run this way behind " +
        "something that is already authenticating requests.");
}
else if (authOptions.GetWeaknessWarning() is { } weakness)
{
    startupLogger.LogWarning("{Warning}", weakness);
}

if (string.IsNullOrWhiteSpace(keyPassphrase))
{
    startupLogger.LogWarning(
        "KONTAINR_KEY_PASSPHRASE is not set, so the Data Protection key ring in {KeysDir} is stored " +
        "unencrypted. Anyone who can read that directory can decrypt every stored SSH, registry and git " +
        "credential. Set KONTAINR_KEY_PASSPHRASE to encrypt the key ring at rest.", keysDir);
}

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

app.UseMiddleware<SecurityHeadersMiddleware>();
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
