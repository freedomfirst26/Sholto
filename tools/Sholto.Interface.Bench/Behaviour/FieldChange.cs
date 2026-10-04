namespace Sholto.Interface.Bench.Behaviour;

/// <summary>One tracked field's value before and after an interaction.</summary>
public sealed class FieldChange
{
    public object? Before { get; init; }
    public object? After { get; init; }
}
