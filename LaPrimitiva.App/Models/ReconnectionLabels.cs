namespace LaPrimitiva.App.Models;

public record ReconnectionLabels
{
    public string Rejoining { get; init; } = string.Empty;
    public string RejoinFailed { get; init; } = string.Empty;
    public string FailedToRejoin { get; init; } = string.Empty;
    public string Retry { get; init; } = string.Empty;
    public string SessionPaused { get; init; } = string.Empty;
    public string Resume { get; init; } = string.Empty;
    public string FailedToResume { get; init; } = string.Empty;
}
