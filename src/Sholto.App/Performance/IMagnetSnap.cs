namespace Sholto.App.Performance;

/// <summary>Magnetic beat-snap: eligibility, the snap factor, and the engaged, release, quantize state machine.</summary>
public interface IMagnetSnap
{
    /// <summary>Raised whenever magnet-lock eligibility flips (the centerline magnet glyph follows it).</summary>
    event Action<bool>? EligibilityChanged;

    /// <summary>0..1: 1 when both decks are playing and their nearest downbeats are in phase, 0 when out of
    /// the magnetic window or when the BPMs are not eligible.</summary>
    double Factor();

    /// <summary>Publish eligibility, run the engaged, release, quantize state machine, and push each deck's
    /// MagneticGlowSec. Reads the factor itself.</summary>
    void Update();
}
