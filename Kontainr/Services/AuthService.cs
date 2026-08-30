using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Kontainr.Services;

/// <summary>
/// Resolved authentication settings for the app.
///
/// Kontainr can start and stop containers, exec a shell inside them, and reach the Docker
/// socket, so an unauthenticated instance is equivalent to remote root on the host. The
/// configuration therefore fails closed: the app refuses to start unless credentials are
/// supplied or the operator explicitly opts out with <c>Auth__Disabled=true</c>.
/// </summary>
public sealed class AuthOptions
{
    /// <summary>Well-known weak passwords carried over from the docs and sample compose file.</summary>
    private static readonly string[] WeakPasswords = ["changeme", "password", "admin", "kontainr"];

    public bool Enabled { get; }
    public string? Username { get; }
    public string? Password { get; }

    private AuthOptions(bool enabled, string? username, string? password)
    {
        Enabled = enabled;
        Username = username;
        Password = password;
    }

    public static AuthOptions Disabled() => new(false, null, null);

    public static AuthOptions Enable(string username, string password) => new(true, username, password);

    /// <summary>
    /// Builds the options from configuration, throwing when the instance would be left
    /// unauthenticated without the operator having asked for it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Credentials are missing or incomplete and <c>Auth:Disabled</c> was not set.
    /// </exception>
    public static AuthOptions FromConfiguration(IConfiguration config)
    {
        var username = config["Auth:Username"];
        var password = config["Auth:Password"];
        var disabled = config.GetValue("Auth:Disabled", false);

        var hasUsername = !string.IsNullOrWhiteSpace(username);
        var hasPassword = !string.IsNullOrWhiteSpace(password);

        if (disabled)
        {
            // An explicit opt-out wins, but flag the contradiction rather than silently
            // ignoring credentials the operator clearly meant to apply.
            if (hasUsername || hasPassword)
                throw new InvalidOperationException(
                    "Auth__Disabled is set to true but Auth__Username/Auth__Password were also supplied. " +
                    "Remove Auth__Disabled to enable authentication, or remove the credentials to run without it.");

            return Disabled();
        }

        if (!hasUsername || !hasPassword)
        {
            var missing = !hasUsername && !hasPassword ? "Auth__Username and Auth__Password are"
                : !hasUsername ? "Auth__Username is"
                : "Auth__Password is";

            throw new InvalidOperationException(
                $"Kontainr will not start unauthenticated: {missing} not set. " +
                "Kontainr controls the Docker socket, so an open instance is equivalent to remote root on this host. " +
                "Set Auth__Username and Auth__Password, or set Auth__Disabled=true to deliberately run with no " +
                "authentication (only appropriate when something in front of Kontainr is already authenticating requests).");
        }

        return Enable(username!, password!);
    }

    /// <summary>
    /// Returns a warning to log about the configured credentials, or null when there is nothing to say.
    /// These are advisory only — a weak password does not block startup.
    /// </summary>
    public string? GetWeaknessWarning()
    {
        if (!Enabled || Password is null) return null;

        if (WeakPasswords.Contains(Password, StringComparer.OrdinalIgnoreCase))
            return "Auth__Password is set to a well-known default. Change it before exposing Kontainr to any untrusted network.";

        if (Password.Length < 12)
            return $"Auth__Password is only {Password.Length} characters. Basic Auth offers no rate limiting by design — use a longer password.";

        return null;
    }
}

public class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AuthOptions _options;

    public BasicAuthMiddleware(RequestDelegate next, AuthOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Allow unauthenticated access to the health endpoint so external monitors can probe it.
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        if (!_options.Enabled)
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
                if (parts.Length == 2 && CredentialsMatch(parts[0], parts[1], _options.Username!, _options.Password!))
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

    /// <summary>
    /// Constant-time string comparison. The inputs are hashed first so that the
    /// comparison always runs over the same number of bytes — FixedTimeEquals
    /// returns early when the spans differ in length, which would otherwise leak
    /// the length of the configured credential.
    /// </summary>
    private static bool FixedTimeEquals(string a, string b)
    {
        Span<byte> aHash = stackalloc byte[32];
        Span<byte> bHash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(a), aHash);
        SHA256.HashData(Encoding.UTF8.GetBytes(b), bHash);
        return CryptographicOperations.FixedTimeEquals(aHash, bHash);
    }
}
