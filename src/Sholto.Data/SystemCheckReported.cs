namespace Sholto.Data;

/// <summary>The boot-time tool check finished. State: a late subscriber is told the result.</summary>
/// <param name="Tools">One entry per external tool the app knows about.</param>
/// <param name="Health">The severity of the check as a whole.</param>
public readonly record struct SystemCheckReported(IReadOnlyList<ToolStatus> Tools, SystemHealth Health) : IStateEvent
{
    public int Slot => 0;
}
