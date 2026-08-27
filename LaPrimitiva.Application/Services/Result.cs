using System;
using LaPrimitiva.Domain.Errors;

namespace LaPrimitiva.Application.Services
{
    public class Result<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public ApplicationError? Error { get; }

        private Result(bool isSuccess, T? value, ApplicationError? error)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
        }

        public static Result<T> Success(T value) => new Result<T>(true, value, null);
        public static Result<T> Failure(ApplicationError error) => new(false, default, error);
    }

    public class Result
    {
        public bool IsSuccess { get; }
        public ApplicationError? Error { get; }

        private Result(bool isSuccess, ApplicationError? error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public static Result Success() => new Result(true, null);
        public static Result Failure(ApplicationError error) => new(false, error);
    }

    public sealed record ApplicationError(
        string Code,
        ErrorCategory Category,
        string Message,
        ErrorRecoveryAction RecoveryAction,
        bool IsRetryable)
    {
        public static ApplicationError FromException(Exception exception)
        {
            if (exception is IErrorException semantic)
            {
                return new ApplicationError(
                    semantic.Error.Code,
                    semantic.Error.Category,
                    semantic.SafeMessage,
                    semantic.Error.RecoveryAction,
                    semantic.Error.IsRetryable);
            }

            var unexpected = ErrorCatalog.Unexpected;
            return new ApplicationError(
                unexpected.Code,
                unexpected.Category,
                unexpected.SafeMessage,
                unexpected.RecoveryAction,
                unexpected.IsRetryable);
        }
    }
}
