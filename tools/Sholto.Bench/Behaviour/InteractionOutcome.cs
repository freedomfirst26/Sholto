namespace Sholto.Bench.Behaviour;

/// <summary>One tracked field's value before and after an interaction.</summary>
public sealed class FieldChange
{
    public object? Before { get; init; }
    public object? After { get; init; }
}

/// <summary>
/// What Bench did and what happened as a result: the answer to "did my click do
/// anything" — the question the <c>ui</c> plane exists to make assertable. Built
/// by diffing a small set of observable VM/deck fields (selection, search-open,
/// per-deck loaded/playing/path) immediately before and after one key or click
/// action. Only fields that actually changed are reported — an outcome with an
/// empty <see cref="Changed"/> means the action ran but visibly did nothing,
/// which is itself the finding a harness must not hide.
/// </summary>
public sealed class InteractionOutcome
{
    public required string Action { get; init; }
    public required string Description { get; init; }
    public IReadOnlyDictionary<string, FieldChange> Changed { get; init; } =
        new Dictionary<string, FieldChange>();
}
