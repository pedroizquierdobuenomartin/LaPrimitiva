using LaPrimitiva.Domain.Errors;
using LaPrimitiva.Domain.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LaPrimitiva.Infrastructure.Persistence;

public static class PersistenceExceptionTranslator
{
    private static readonly HashSet<int> UnavailableSqlServerErrors =
    [
        -2, 2, 20, 53, 64, 233, 4060, 10053, 10054, 10060, 10061, 11001
    ];

    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        Guid? entityId = null)
    {
        try
        {
            return await operation();
        }
        catch (Exception exception)
        {
            var translated = Translate(exception, operationName, entityId);
            if (ReferenceEquals(translated, exception))
            {
                throw;
            }

            throw translated;
        }
    }

    public static async Task ExecuteAsync(
        Func<Task> operation,
        string operationName,
        Guid? entityId = null)
    {
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            var translated = Translate(exception, operationName, entityId);
            if (ReferenceEquals(translated, exception))
            {
                throw;
            }

            throw translated;
        }
    }

    public static Exception Translate(Exception exception, string operationName, Guid? entityId = null)
    {
        if (exception is OperationCanceledException or IErrorException)
        {
            return exception;
        }

        if (exception is DbUpdateConcurrencyException && entityId.HasValue)
        {
            return new ConcurrencyConflictException(entityId.Value, exception);
        }

        if (FindSqlException(exception) is { } sqlException)
        {
            return TranslateSqlServerError(sqlException.Number, operationName, sqlException);
        }

        if (exception is DbUpdateException)
        {
            return new PersistenceOperationException(operationName, exception);
        }

        return exception;
    }

    public static Exception TranslateSqlServerError(
        int errorNumber,
        string operationName,
        Exception? innerException = null)
    {
        var context = new Dictionary<string, object?>
        {
            ["Operation"] = operationName,
            ["ProviderErrorNumber"] = errorNumber
        };

        return errorNumber switch
        {
            2601 or 2627 => new DataIntegrityException(
                "persistence.unique-constraint",
                "Ya existe un registro con los mismos datos únicos.",
                context,
                innerException),
            547 => new DataIntegrityException(
                "persistence.referential-integrity",
                "La operación entra en conflicto con datos relacionados.",
                context,
                innerException),
            _ when UnavailableSqlServerErrors.Contains(errorNumber) =>
                new PersistenceUnavailableException(operationName, innerException, errorNumber),
            _ => new PersistenceOperationException(operationName, innerException, errorNumber)
        };
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException)
            {
                return sqlException;
            }
        }

        return null;
    }
}
