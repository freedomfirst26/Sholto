namespace Sholto.ExternalTools;

/// <summary>
/// The executable name of every arm's-length tool Sholto launches, written once.
///
/// Why this exists: the names were scattered — <c>"ffmpeg"</c> appeared twice on a
/// single line in <c>FfmpegDecodeStrategy</c> (once to resolve, once as the fallback),
/// and <c>DBNDownBeatTracker</c> was written in its tool descriptor AND again in the
/// analyser's <c>BinaryPath</c> property. A rename or a platform-specific suffix had
/// to be applied in several places, and missing one is silent — the tool simply
/// isn't found.
///
/// These are the names as invoked, not display names: <see cref="Madmom"/> is the
/// beat-tracker entry point rather than the package (<c>madmom-onnx</c>), which is why
/// the constant and its value differ.
///
/// Nothing but consts and <see cref="All"/> lives here — which tools exist, where to
/// find them, and what to do with each is <see cref="ExternalToolOptions"/> and the
/// composition root's job, not this type's.
///
/// <para><b>Windows:</b> these are bare names with no <c>.exe</c> suffix.</para>
///
/// <para><c>pactl</c> and <c>pw-link</c> are deliberately absent: <c>PipeWireRouter</c>
/// still runs them through its own private helpers, and folding it into this layer
/// changes argument quoting on the audio path, so it is a separate follow-up.</para>
/// </summary>
public static class ExternalToolNames
{
    /// <summary>Beat and downbeat detection. Required — a track cannot play without a
    /// beatgrid. Shipped by the <c>madmom-onnx</c> package.</summary>
    public const string Madmom = "DBNDownBeatTracker";

    /// <summary>Stem separation (drums / vocals / bass / other). Optional.</summary>
    public const string Demucs = "demucs";

    /// <summary>Audio transcoding — the M4A/AAC decode path, and test-signal generation
    /// for the installer's health check. Not run through
    /// <see cref="ExternalToolRunner"/>: it is a streaming decoder whose stdout is
    /// consumed as a live pipe.</summary>
    public const string Ffmpeg = "ffmpeg";

    /// <summary>Every tool name above, in one place — the default for
    /// <see cref="ExternalToolOptions.Tools"/>. Adding a fifth tool is one const here
    /// plus one entry in this list, not a new property to thread through everything
    /// that reads <see cref="ExternalToolOptions"/>.</summary>
    public static readonly IReadOnlyList<string> All = [Madmom, Demucs, Ffmpeg];
}
