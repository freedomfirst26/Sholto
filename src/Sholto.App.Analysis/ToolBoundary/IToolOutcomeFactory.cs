namespace Sholto.App.Analysis.ToolBoundary;

/// <summary>Builds <see cref="ToolOutcome{T}"/> values — the only way to make one.</summary>
public interface IToolOutcomeFactory
{
    ToolOutcome<T> Success<T>(T value, string? message = null);

    ToolOutcome<T> Failure<T>(string reason);
}
