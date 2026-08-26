namespace LaPrimitiva.Application.Interfaces;

public interface IApplicationErrorReporter
{
    void Report(Exception exception, string operation);
}
