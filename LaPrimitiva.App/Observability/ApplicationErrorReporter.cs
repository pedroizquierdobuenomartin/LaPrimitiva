using LaPrimitiva.Application.Interfaces;

namespace LaPrimitiva.App.Observability;

public sealed class ApplicationErrorReporter(ILogger<ApplicationErrorReporter> logger)
    : IApplicationErrorReporter
{
    public void Report(Exception exception, string operation) =>
        logger.LogError(exception, "Falló la operación {Operation}.", operation);
}
