namespace Sholto.App.Dsp;

/// <summary>Crossfade curve: maps a 0..1 crossfader position to the per-deck gains.
/// Implementations choose the curve (equal-power, linear, cut...). Injected so the UI
/// and the Bench scenario runner share one curve.</summary>
public interface ICrossfadeCurve
{
    /// <param name="position">0..1, 0 = full deck A, 1 = full deck B.</param>
    /// <param name="gainA">Gain for the deck at position 0.</param>
    /// <param name="gainB">Gain for the deck at position 1.</param>
    void ComputeGains(double position, out float gainA, out float gainB);
}
