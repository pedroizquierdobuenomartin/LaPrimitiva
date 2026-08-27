using LaPrimitiva.Domain.Errors;

namespace LaPrimitiva.Domain.Exceptions;

public sealed class ConcurrencyConflictException(Guid entityId, Exception? innerException = null)
    : InvalidOperationException(ErrorCatalog.ConcurrencyConflict.SafeMessage, innerException), IErrorException
{
    public Guid EntityId { get; } = entityId;
    public ErrorDefinition Error => ErrorCatalog.ConcurrencyConflict;
    public string SafeMessage => Error.SafeMessage;
    public IReadOnlyDictionary<string, object?> Context { get; } =
        new Dictionary<string, object?> { ["EntityId"] = entityId };
}
