namespace Sholto.Data;

/// <summary>A <see cref="Channel{T}"/> for a state event: remembers the last value per slot. The
/// constraint lets <c>Slot</c> be read without boxing the struct.</summary>
internal sealed class StateChannel<T> : Channel<T> where T : struct, IStateEvent
{
    private readonly Dictionary<int, T> _last = [];

    public override void Record(in T e) => _last[e.Slot] = e;

    public override T[] Snapshot() => [.. _last.Values];
}
