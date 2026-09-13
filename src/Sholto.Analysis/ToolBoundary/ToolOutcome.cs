namespace Sholto.Analysis.ToolBoundary;

/// <summary>
/// The result of verifying an external tool's postcondition. Exactly two states —
/// success carries the parsed/verified result, failure carries why. No third state:
/// there is no way to be "done" without having gone through one of these two, which
/// is what makes it structurally impossible to report Complete on exit code alone.
/// </summary>
public sealed class ToolOutcome<T>
{
    public bool IsSuccess { get; }

    /// <summary>Set only when <see cref="IsSuccess"/> is true.</summary>
    public T? Value { get; }

    /// <summary>Optional human-readable success message (e.g. "12 sections"). Falls
    /// back to a generic "ok" at the call site when null.</summary>
    public string? Message { get; }

    /// <summary>Set only when <see cref="IsSuccess"/> is false — why the postcondition
    /// was not met.</summary>
    public string? Reason { get; }

    private ToolOutcome(bool isSuccess, T? value, string? message, string? reason)
    {
        IsSuccess = isSuccess;
        Value = value;
        Message = message;
        Reason = reason;
    }

    public static ToolOutcome<T> Success(T value, string? message = null) =>
        new(true, value, message, null);

    public static ToolOutcome<T> Failure(string reason) =>
        new(false, default, null, reason);
}
