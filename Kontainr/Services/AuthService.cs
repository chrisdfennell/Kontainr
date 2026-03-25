using System.Net;
using System.Text;

namespace Kontainr.Services;

public class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string? _username;
    private readonly string? _password;

    public BasicAuthMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _username = config["Auth:Username"];
        _password = config["Auth:Password"];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip auth if no credentials configured
        if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            var encoded = authHeader["Basic ".Length..].Trim();
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                var parts = decoded.Split(':', 2);
                if (parts.Length == 2 && parts[0] == _username && parts[1] == _password)
                {
                    await _next(context);
                    return;
                }
            }
            catch { }
        }

        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"Kontainr\"";
        await context.Response.WriteAsync("Unauthorized");
    }
}
