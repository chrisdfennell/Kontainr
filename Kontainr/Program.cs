using Kontainr.Components;
using Kontainr.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<DockerService>();
builder.Services.AddSingleton<ToastService>();

var app = builder.Build();

// Basic auth — only active when Auth:Username and Auth:Password are set
// Set via env vars: Auth__Username and Auth__Password
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
