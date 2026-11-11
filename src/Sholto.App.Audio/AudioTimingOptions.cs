namespace Sholto.App.Audio;

/// <summary>Audio-callback diagnostics, supplied via the standard <c>IOptions&lt;AudioTimingOptions&gt;</c>
/// pipeline. The Host sets <see cref="Enabled"/> from the <c>SHOLTO_AUDIO_TIMING=1</c> environment
/// variable (<c>AudioTimingOptionsFactory</c>).</summary>
public sealed class AudioTimingOptions
{
    /// <summary>Record a histogram of how long each output-router callback takes. Off by default:
    /// when off the router allocates and measures nothing.</summary>
    public bool Enabled { get; set; }
}
