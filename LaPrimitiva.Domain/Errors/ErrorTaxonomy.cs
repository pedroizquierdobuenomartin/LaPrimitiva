namespace LaPrimitiva.Domain.Errors;

public enum ErrorCategory
{
    BusinessRule,
    NotFound,
    Concurrency,
    Integrity,
    PersistenceUnavailable,
    ExternalUnavailable,
    ExternalInvalidData,
    Unexpected
}

public enum ErrorSeverity
{
    Information,
    Warning,
    Error,
    Critical
}

public enum ErrorRecoveryAction
{
    CorrectInput,
    Retry,
    Reload,
    GoBack
}

public sealed record ErrorDefinition(
    string Code,
    ErrorCategory Category,
    ErrorSeverity Severity,
    bool IsRetryable,
    ErrorRecoveryAction RecoveryAction,
    string SafeMessage);

public static class ErrorCatalog
{
    public static ErrorDefinition BusinessRule { get; } = new(
        "business.rule",
        ErrorCategory.BusinessRule,
        ErrorSeverity.Warning,
        false,
        ErrorRecoveryAction.CorrectInput,
        "Revisa los datos indicados y corrige los campos marcados.");

    public static ErrorDefinition EntityNotFound { get; } = new(
        "entity.not-found",
        ErrorCategory.NotFound,
        ErrorSeverity.Warning,
        false,
        ErrorRecoveryAction.GoBack,
        "El elemento solicitado ya no está disponible.");

    public static ErrorDefinition ConcurrencyConflict { get; } = new(
        "persistence.concurrency",
        ErrorCategory.Concurrency,
        ErrorSeverity.Warning,
        false,
        ErrorRecoveryAction.Reload,
        "Otro usuario ha modificado este registro. Recarga los datos antes de volver a guardar.");

    public static ErrorDefinition DataIntegrity { get; } = new(
        "persistence.integrity",
        ErrorCategory.Integrity,
        ErrorSeverity.Warning,
        false,
        ErrorRecoveryAction.CorrectInput,
        "Los datos entran en conflicto con información ya registrada.");

    public static ErrorDefinition PersistenceUnavailable { get; } = new(
        "persistence.unavailable",
        ErrorCategory.PersistenceUnavailable,
        ErrorSeverity.Error,
        true,
        ErrorRecoveryAction.Retry,
        "No se puede acceder a los datos en este momento. Inténtalo de nuevo.");

    public static ErrorDefinition ExternalUnavailable { get; } = new(
        "external.unavailable",
        ErrorCategory.ExternalUnavailable,
        ErrorSeverity.Warning,
        true,
        ErrorRecoveryAction.Retry,
        "El servicio externo no está disponible en este momento. Inténtalo de nuevo más tarde.");

    public static ErrorDefinition ExternalInvalidData { get; } = new(
        "external.invalid-data",
        ErrorCategory.ExternalInvalidData,
        ErrorSeverity.Warning,
        true,
        ErrorRecoveryAction.Retry,
        "El servicio externo devolvió datos que no se pueden procesar.");

    public static ErrorDefinition Unexpected { get; } = new(
        "unexpected",
        ErrorCategory.Unexpected,
        ErrorSeverity.Error,
        false,
        ErrorRecoveryAction.GoBack,
        "Ha ocurrido un error inesperado. El detalle técnico se ha registrado de forma segura.");
}

public interface IErrorException
{
    ErrorDefinition Error { get; }
    string SafeMessage { get; }
    IReadOnlyDictionary<string, object?> Context { get; }
}

public sealed class BusinessRuleException : InvalidOperationException, IErrorException
{
    public BusinessRuleException(
        string ruleCode,
        string safeMessage,
        IReadOnlyDictionary<string, object?>? context = null)
        : base(safeMessage)
    {
        RuleCode = ruleCode;
        SafeMessage = safeMessage;
        Context = ErrorContext.With(context, ("RuleCode", ruleCode));
    }

    public string RuleCode { get; }
    public ErrorDefinition Error => ErrorCatalog.BusinessRule;
    public string SafeMessage { get; }
    public IReadOnlyDictionary<string, object?> Context { get; }
}

public sealed class EntityNotFoundException : InvalidOperationException, IErrorException
{
    public EntityNotFoundException(string entityName, object entityId)
        : base(ErrorCatalog.EntityNotFound.SafeMessage)
    {
        EntityName = entityName;
        EntityId = entityId;
        Context = ErrorContext.With(null, ("EntityName", entityName), ("EntityId", entityId));
    }

    public string EntityName { get; }
    public object EntityId { get; }
    public ErrorDefinition Error => ErrorCatalog.EntityNotFound;
    public string SafeMessage => Error.SafeMessage;
    public IReadOnlyDictionary<string, object?> Context { get; }
}

public sealed class DataIntegrityException : InvalidOperationException, IErrorException
{
    public DataIntegrityException(
        string integrityCode,
        string? safeMessage = null,
        IReadOnlyDictionary<string, object?>? context = null,
        Exception? innerException = null)
        : base(safeMessage ?? ErrorCatalog.DataIntegrity.SafeMessage, innerException)
    {
        IntegrityCode = integrityCode;
        SafeMessage = safeMessage ?? ErrorCatalog.DataIntegrity.SafeMessage;
        Context = ErrorContext.With(context, ("IntegrityCode", integrityCode));
    }

    public string IntegrityCode { get; }
    public ErrorDefinition Error => ErrorCatalog.DataIntegrity;
    public string SafeMessage { get; }
    public IReadOnlyDictionary<string, object?> Context { get; }
}

public sealed class PersistenceUnavailableException : Exception, IErrorException
{
    public PersistenceUnavailableException(
        string operation,
        Exception? innerException = null,
        int? providerErrorNumber = null)
        : base(ErrorCatalog.PersistenceUnavailable.SafeMessage, innerException)
    {
        Operation = operation;
        ProviderErrorNumber = providerErrorNumber;
        Context = ErrorContext.With(
            null,
            ("Operation", operation),
            ("ProviderErrorNumber", providerErrorNumber));
    }

    public string Operation { get; }
    public int? ProviderErrorNumber { get; }
    public ErrorDefinition Error => ErrorCatalog.PersistenceUnavailable;
    public string SafeMessage => Error.SafeMessage;
    public IReadOnlyDictionary<string, object?> Context { get; }
}

public sealed class PersistenceOperationException : Exception, IErrorException
{
    public PersistenceOperationException(
        string operation,
        Exception? innerException = null,
        int? providerErrorNumber = null)
        : base(ErrorCatalog.Unexpected.SafeMessage, innerException)
    {
        Operation = operation;
        ProviderErrorNumber = providerErrorNumber;
        Context = ErrorContext.With(
            null,
            ("Operation", operation),
            ("ProviderErrorNumber", providerErrorNumber));
    }

    public string Operation { get; }
    public int? ProviderErrorNumber { get; }
    public ErrorDefinition Error => ErrorCatalog.Unexpected with { Code = "persistence.failed" };
    public string SafeMessage => Error.SafeMessage;
    public IReadOnlyDictionary<string, object?> Context { get; }
}

public sealed class ExternalServiceUnavailableException : Exception, IErrorException
{
    public ExternalServiceUnavailableException(string provider, Exception? innerException = null)
        : base(ErrorCatalog.ExternalUnavailable.SafeMessage, innerException)
    {
        Provider = provider;
        Context = ErrorContext.With(null, ("Provider", provider));
    }

    public string Provider { get; }
    public ErrorDefinition Error => ErrorCatalog.ExternalUnavailable;
    public string SafeMessage => Error.SafeMessage;
    public IReadOnlyDictionary<string, object?> Context { get; }
}

public sealed class ExternalServiceTimeoutException : TimeoutException, IErrorException
{
    public ExternalServiceTimeoutException(string provider, TimeSpan timeout, Exception? innerException = null)
        : base("El servicio externo ha tardado demasiado en responder. Inténtalo de nuevo.", innerException)
    {
        Provider = provider;
        Timeout = timeout;
        Context = ErrorContext.With(null, ("Provider", provider), ("TimeoutSeconds", timeout.TotalSeconds));
    }

    public string Provider { get; }
    public TimeSpan Timeout { get; }
    public ErrorDefinition Error => ErrorCatalog.ExternalUnavailable with { Code = "external.timeout" };
    public string SafeMessage => Message;
    public IReadOnlyDictionary<string, object?> Context { get; }
}

public sealed class ExternalDataFormatException : Exception, IErrorException
{
    public ExternalDataFormatException(
        string provider,
        string formatCode,
        Exception? innerException = null)
        : base(ErrorCatalog.ExternalInvalidData.SafeMessage, innerException)
    {
        Provider = provider;
        FormatCode = formatCode;
        Context = ErrorContext.With(null, ("Provider", provider), ("FormatCode", formatCode));
    }

    public string Provider { get; }
    public string FormatCode { get; }
    public ErrorDefinition Error => ErrorCatalog.ExternalInvalidData;
    public string SafeMessage => Error.SafeMessage;
    public IReadOnlyDictionary<string, object?> Context { get; }
}

internal static class ErrorContext
{
    public static IReadOnlyDictionary<string, object?> With(
        IReadOnlyDictionary<string, object?>? source,
        params (string Key, object? Value)[] values)
    {
        var context = source is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(source);

        foreach (var (key, value) in values)
        {
            context[key] = value;
        }

        return context;
    }
}
