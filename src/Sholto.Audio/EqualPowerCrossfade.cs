namespace Sholto.Audio;

/// <summary>
/// Equal-power crossfade curve: cosine/sine gains so perceived loudness stays
/// flat through the centre instead of dipping like a linear crossfade would.
/// Shared by <c>MainViewModel.Crossfader</c> (UI) and
/// <c>Sholto.Bench.Scenario.ScenarioRunner</c>'s "crossfader" action so both
/// compute the exact same gains from the same 0..1 position.
/// </summary>
public static class EqualPowerCrossfade
{
    /// <param name="position">0..1, 0 = full deck A, 1 = full deck B.</param>
    /// <param name="gainA">Cosine gain for the deck at position 0.</param>
    /// <param name="gainB">Sine gain for the deck at position 1.</param>
    public static void ComputeGains(double position, out float gainA, out float gainB)
    {
        double angle = position * (Math.PI / 2);
        gainA = (float)Math.Cos(angle);
        gainB = (float)Math.Sin(angle);
    }
}
