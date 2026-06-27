using System.Net;
using System.Security.Cryptography;
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
        // Allow unauthenticated access to the health endpoint so external monitors can probe it.
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

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
                if (parts.Length == 2 && CredentialsMatch(parts[0], parts[1], _username!, _password!))
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

    /// <summary>
    /// Compares supplied credentials against the configured ones in constant time
    /// to avoid leaking length/content via timing side-channels. Both fields are
    /// always compared (no short-circuit) so timing does not reveal which one failed.
    /// </summary>
    internal static bool CredentialsMatch(string suppliedUser, string suppliedPass, string expectedUser, string expectedPass)
    {
        var userOk = FixedTimeEquals(suppliedUser, expectedUser);
        var passOk = FixedTimeEquals(suppliedPass, expectedPass);
        return userOk & passOk;
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
