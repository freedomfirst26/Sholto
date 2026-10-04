using Sholto.Data;

namespace Sholto.App.Performance;

/// <summary>The platter physics: grab, move, fling, coast, brake, and the repair of a touch that Inspect
/// stranded. Leaving Inspect (<see cref="InspectModeChanged"/> off) releases stranded touches.</summary>
public interface IScratchEngine : IEventHandler<InspectModeChanged>, IDisposable
{
    /// <summary>The engine's state for a deck (0 or 1).</summary>
    ScratchState StateOf(int deck);

    /// <summary>The platter's touch sensor, both edges. Shift + touch is the silent fast-search, not a grab.</summary>
    void Touch(int deck, bool touching, bool shifted);

    /// <summary>The top platter turned on a scratch-capable deck: grab if not already scratching, then
    /// accumulate the ticks for the next frame.</summary>
    void Turn(int deck, int delta);

    /// <summary>One frame for one deck: turn accumulated ticks into a varispeed rate, or coast and release.</summary>
    void Tick(int deck, DateTime now);

    /// <summary>Repairs a platter grab that Inspect mode stranded: the lift never arrived, so clear
    /// <see cref="ScratchState.Touching"/> on every active deck and let the normal release decay take over.</summary>
    void ReleaseStrandedTouches();
}
