namespace LaPrimitiva.Application.Interfaces;

public interface IApplicationErrorReporter
{
    string Report(Exception exception, string operation);
}
