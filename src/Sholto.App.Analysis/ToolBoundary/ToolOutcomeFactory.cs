namespace Sholto.App.Analysis.ToolBoundary;

public sealed class ToolOutcomeFactory : IToolOutcomeFactory
{
    public ToolOutcome<T> Success<T>(T value, string? message = null) =>
        new(true, value, message, null);

    public ToolOutcome<T> Failure<T>(string reason) =>
        new(false, default, null, reason);
}
