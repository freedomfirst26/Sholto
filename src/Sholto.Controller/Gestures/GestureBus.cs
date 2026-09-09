namespace Sholto.Controller.Gestures;

/// <summary>Delivers each gesture to every registered consumer, in registration order.
/// <para>Order is a contract, not an accident: the App registers first and acts, then
/// observers watch. An observer must never mutate app state.</para></summary>
public sealed class GestureBus
{
    private readonly List<GestureBindings> _tables = [];

    /// <summary>Raised when a consumer's delegate throws. The bus swallows the exception
    /// so one misbehaving consumer cannot stop the others — a crash in the guide overlay
    /// must never stop the decks responding.</summary>
    public event Action<GestureBindings, Gesture, Exception>? HandlerFailed;

    public void Register(GestureBindings table) => _tables.Add(table);
    public void Unregister(GestureBindings table) => _tables.Remove(table);

    public void Dispatch(Gesture gesture)
    {
        // Snapshot: a handler may register or unregister during dispatch.
        foreach (var table in _tables.ToArray())
        {
            try { table.Invoke(gesture); }
            catch (Exception ex) { HandlerFailed?.Invoke(table, gesture, ex); }
        }
    }
}
