namespace Sholto.App.Performance;

/// <summary>The jog paths' side of jog recency: stamp "this deck was just jogged", or expire the stamps
/// when a scratch ends.</summary>
public interface IJogRecencyWriter
{
    /// <param name="deck">Zero-based deck index (0 or 1), as the platter commands carry it.</param>
    void MarkJogged(int deck);

    /// <summary>A scratch ending is NOT a jog release: expire the recency stamps so the magnetic quantize
    /// (armed by "recently jogged, now idle") does not misfire off the coast-to-rest that follows a scratch.</summary>
    /// <param name="isDeck1">Which deck the scratch that just ended was on.</param>
    void ClearAfterScratchEnd(bool isDeck1);
}
