namespace Sholto.App.Analysis.Analyzers.Keys;

/// <summary>Tunable settings for <see cref="KeyAnalyzer"/>, supplied via the standard
/// <c>IOptions&lt;KeyAnalysisOptions&gt;</c> pipeline. Defaults reproduce the key the
/// analyzer has always found.</summary>
public sealed class KeyAnalysisOptions
{
    /// <summary>Concert pitch of A4 (MIDI note 69) in Hz, from which every Goertzel
    /// filter frequency is derived. 440 Hz is standard tuning.</summary>
    public double ReferencePitchHz { get; set; } = 440.0;

    /// <summary>Lowest MIDI note the filter bank listens to. 36 is C2.</summary>
    public int FirstMidiNote { get; set; } = 36;

    /// <summary>Highest MIDI note the filter bank listens to, inclusive. 95 is B6, so the
    /// default bank covers five whole octaves (60 filters). Must not be below
    /// <see cref="FirstMidiNote"/>.</summary>
    public int LastMidiNote { get; set; } = 95;

    /// <summary>How many 4096-sample frames one parallel work chunk sums before its
    /// partial chroma is merged. Only affects how the work is split across cores.</summary>
    public int FramesPerChunk { get; set; } = 32;
}
