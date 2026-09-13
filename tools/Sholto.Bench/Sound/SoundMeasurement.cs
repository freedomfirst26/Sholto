namespace Sholto.Bench.Sound;

// The "sound" sense: what Bench heard when it rendered a scenario through
// CueOutputRouter and measured the WAV with ffmpeg. See Sholto.Bench.Appearance
// for "look" and Sholto.Bench.Behaviour for "behaviour" — three distinct senses,
// three distinct result families, deliberately not unified behind a shared base
// type (they answer different questions and share nothing but "Bench made this").

/// <summary>What Bench heard: loudness, per-channel levels and silences for one
/// rendered WAV. Named for the sense it carries — "a measurement" alone doesn't
/// say of what, once Bench also produces look (<c>AppearanceCapture</c>) and
/// behaviour (<c>InteractionOutcome</c>/<c>BenchState</c>) results.</summary>
public sealed class SoundMeasurement
{
    public string WavPath { get; init; } = "";
    public double? DurationSeconds { get; init; }
    public LoudnessResult Loudness { get; init; } = new();
    public IReadOnlyList<ChannelStats> Channels { get; init; } = [];
    public IReadOnlyList<SilenceInterval> Silences { get; init; } = [];
}
