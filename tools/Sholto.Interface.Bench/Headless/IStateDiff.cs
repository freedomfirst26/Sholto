using Sholto.Interface.Bench.Behaviour;

namespace Sholto.Interface.Bench.Headless;

/// <summary>Finds which snapshot fields differ between two takes.</summary>
public interface IStateDiff
{
    Dictionary<string, FieldChange> Between(Dictionary<string, object?> before, Dictionary<string, object?> after);
}
