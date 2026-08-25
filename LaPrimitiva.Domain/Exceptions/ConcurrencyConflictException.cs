namespace LaPrimitiva.Domain.Exceptions;

public sealed class ConcurrencyConflictException(Guid entityId, Exception? innerException = null)
    : InvalidOperationException(
        "Otro usuario ha modificado este registro mientras lo editabas. Recarga los datos antes de volver a guardar.",
        innerException)
{
    public Guid EntityId { get; } = entityId;
}
