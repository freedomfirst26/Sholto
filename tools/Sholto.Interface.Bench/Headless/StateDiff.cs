using Sholto.Interface.Bench.Behaviour;

namespace Sholto.Interface.Bench.Headless;

/// <summary>Only the fields that actually changed are reported: the record of "did this do anything",
/// not just "did it throw".</summary>
public sealed class StateDiff : IStateDiff
{
    public Dictionary<string, FieldChange> Between(Dictionary<string, object?> before, Dictionary<string, object?> after)
    {
        var changed = new Dictionary<string, FieldChange>();
        foreach (var (key, beforeValue) in before)
        {
            var afterValue = after[key];
            if (!Equals(beforeValue, afterValue))
                changed[key] = new FieldChange { Before = beforeValue, After = afterValue };
        }
        return changed;
    }
}
