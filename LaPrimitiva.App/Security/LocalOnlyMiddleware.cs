namespace LaPrimitiva.App.Security;

public sealed class LocalOnlyMiddleware(RequestDelegate next, ILogger<LocalOnlyMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var remoteAddress = context.Connection.RemoteIpAddress;

        if (!LocalOnlyPolicy.IsLoopbackAddress(remoteAddress))
        {
            logger.LogWarning(
                "Petición rechazada por la política local. IP remota: {RemoteAddress}; Host: {Host}",
                remoteAddress,
                context.Request.Host.Value);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Esta aplicación solo admite acceso desde el equipo local.");
            return;
        }

        await next(context);
    }
}
