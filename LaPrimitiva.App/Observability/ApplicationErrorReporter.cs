using LaPrimitiva.Application.Interfaces;
using LaPrimitiva.Domain.Errors;
using System.Diagnostics;

namespace LaPrimitiva.App.Observability;

public sealed class ApplicationErrorReporter(ILogger<ApplicationErrorReporter> logger)
    : IApplicationErrorReporter
{
    public string Report(Exception exception, string operation)
    {
        var error = exception is IErrorException semantic
            ? semantic.Error
            : ErrorCatalog.Unexpected;
        var reference = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["ErrorReference"] = reference,
            ["ErrorCode"] = error.Code,
            ["ErrorCategory"] = error.Category.ToString(),
            ["Operation"] = operation
        }))
        {
            if (error.Severity is ErrorSeverity.Information or ErrorSeverity.Warning)
            {
                logger.LogWarning(exception, "Falló la operación {Operation} con {ErrorCode}.", operation, error.Code);
            }
            else
            {
                logger.LogError(exception, "Falló la operación {Operation} con {ErrorCode}.", operation, error.Code);
            }
        }

        return reference;
    }
}
