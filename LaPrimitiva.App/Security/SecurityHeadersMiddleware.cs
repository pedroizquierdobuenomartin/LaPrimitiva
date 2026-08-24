namespace LaPrimitiva.App.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicyPrefix =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "font-src 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'; " +
        "img-src 'self' data:; " +
        "object-src 'none'; " +
        "script-src 'self'; " +
        "style-src 'self'";

    public Task InvokeAsync(HttpContext context)
    {
        var webSocketScheme = context.Request.IsHttps ? "wss" : "ws";
        var webSocketSource = $"{webSocketScheme}://{context.Request.Host.ToUriComponent()}";

        context.Response.Headers["Content-Security-Policy"] =
            $"{ContentSecurityPolicyPrefix}; connect-src 'self' {webSocketSource}";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";

        return next(context);
    }
}
