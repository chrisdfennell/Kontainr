using Kontainr.Components;
using Kontainr.Data;
using Kontainr.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

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

// Ensure metrics database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MetricsDbContext>>().CreateDbContext();
    db.Database.EnsureCreated();
}

app.UseMiddleware<BasicAuthMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
