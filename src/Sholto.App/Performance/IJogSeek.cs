namespace Sholto.App.Performance;

/// <summary>Coalesced silent seeks: jog turns accumulate per deck and are flushed as one seek per deck per
/// frame (each seek flushes the audio buffer, so per-event seeks at ~100 Hz would glitch). Also owns the
/// per-deck scrub flag.</summary>
public interface IJogSeek
{
    /// <summary>Add <paramref name="seconds"/> of seek to a deck's (0 or 1) pending total. Allocates nothing.</summary>
    void Add(int deck, double seconds);

    /// <summary>Track-seconds waiting to be flushed for a deck (0 or 1).</summary>
    double PendingSeconds(int deck);

    /// <summary>Issue one relative seek per deck with pending work, scaled by <paramref name="scale"/>
    /// (magnetic beat-snap damps the jog), and clear the pending totals.</summary>
    void Flush(double scale);

    /// <summary>Set each deck's IsScrubbing: it was the last-jogged deck and the jog is within the active window.</summary>
    void UpdateScrubbing(DateTime now);
}
