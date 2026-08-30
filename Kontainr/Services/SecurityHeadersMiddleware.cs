namespace Kontainr.Services;

/// <summary>
/// Adds the response headers that browsers rely on to contain a compromised or
/// hostile page. Kontainr renders one-click destructive actions (stop, remove,
/// prune, exec), so framing protection in particular is doing real work here.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _contentSecurityPolicy;

    public SecurityHeadersMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;

        // xterm.js and cytoscape are loaded from jsDelivr by Components/App.razor.
        //
        // 'unsafe-inline' is currently required for both directives: the Razor components
        // carry ~150 inline event handlers (onclick="kontainr...") and several hundred
        // inline style attributes. So this policy is not yet an XSS control — what it does
        // buy is frame-ancestors (clickjacking, which matters when every destructive action
        // is one click), object-src, base-uri and form-action. Migrating the inline handlers
        // to Blazor @onclick would let 'unsafe-inline' drop out of script-src.
        var scriptSrc = "'self' 'unsafe-inline' https://cdn.jsdelivr.net";
        var styleSrc = "'self' 'unsafe-inline' https://cdn.jsdelivr.net";

        _contentSecurityPolicy = config["Security:ContentSecurityPolicy"] ?? string.Join("; ",
        [
            "default-src 'self'",
            $"script-src {scriptSrc}",
            $"style-src {styleSrc}",
            "img-src 'self' data:",
            "font-src 'self' data: https://cdn.jsdelivr.net",
            // Blazor Server's circuit runs over a same-origin WebSocket.
            "connect-src 'self' ws: wss:",
            "frame-ancestors 'none'",
            "frame-src 'none'",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'"
        ]);
    }

    public Task InvokeAsync(HttpContext context)
    {
        // Applied as the response starts rather than up front: UseExceptionHandler clears the
        // response before re-executing to /Error, which would otherwise drop these headers from
        // exactly the responses (unhandled errors) where they matter most.
        context.Response.OnStarting(static state =>
        {
            ((State)state).Apply();
            return Task.CompletedTask;
        }, new State(this, context));

        return _next(context);
    }

    private void ApplyHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["Content-Security-Policy"] = _contentSecurityPolicy;
        // frame-ancestors covers modern browsers; X-Frame-Options covers the rest.
        headers["X-Frame-Options"] = "DENY";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), interest-cohort=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";

        // Only meaningful over TLS, and only safe to send when the request actually
        // arrived over TLS — most deployments terminate TLS at a reverse proxy.
        if (context.Request.IsHttps)
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    /// <summary>Boxed once per request so the OnStarting callback can stay static.</summary>
    private sealed class State(SecurityHeadersMiddleware middleware, HttpContext context)
    {
        public void Apply() => middleware.ApplyHeaders(context);
    }
}
