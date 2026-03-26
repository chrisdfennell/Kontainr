using Kontainr.Components;
using Kontainr.Services;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dataDir = builder.Configuration["KONTAINR_DATA"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataDir);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")));

builder.Services.AddSingleton<DockerService>();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddSingleton<SshSettingsService>();
builder.Services.AddSingleton<SshSessionManager>();
builder.Services.AddSingleton<StatsHistoryService>();
builder.Services.AddSingleton<AuditService>();
builder.Services.AddSingleton<WebhookService>();
builder.Services.AddSingleton<ContainerEventService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ContainerEventService>());
builder.Services.AddSingleton<ContainerFileService>();
builder.Services.AddSingleton<RegistryService>();
builder.Services.AddSingleton<ScheduledRestartService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ScheduledRestartService>());

var app = builder.Build();

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
