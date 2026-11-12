using Sholto.App.Analysis.Analyzers.Beats;

namespace Sholto.App.ExternalTools;

/// <summary>Turns a beat tracker's <c>TIME\tBEAT_NUMBER</c> lines into <see cref="DetectedBeats"/>.
/// Shared by every beat-tracker descriptor (Beat This!, madmom): both CLIs emit the same format.</summary>
public interface IDetectedBeatsFactory
{
    /// <summary>Every parsable line's time is a beat; lines whose beat number is 1 are also
    /// downbeats; the tempo is <see cref="BpmFromBeats"/>. Lines that do not start with a
    /// number are skipped, so no beats at all comes back as empty arrays, never a throw.</summary>
    DetectedBeats FromBeatLines(string text);

    /// <summary>Tempo (BPM, 0.1 resolution) from beat times by the trimmed mean of the gaps.</summary>
    double BpmFromBeats(IReadOnlyList<double> beats);
}
