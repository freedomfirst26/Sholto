namespace Sholto.Data;

/// <summary>An event describing current state. The bus keeps the last value per
/// (event type, <see cref="Slot"/>) and delivers it to a new subscriber immediately on Subscribe,
/// so a replugged device or a freshly mounted view catches up without asking.</summary>
public interface IStateEvent : IEvent
{
    /// <summary>Which instance of the state this describes (e.g. deck index). Use 0 for singletons.</summary>
    int Slot { get; }
}
