namespace Sholto.Host;

/// <summary>Stem separation settings supplied via the standard <c>IOptions&lt;StemSeparationOptions&gt;</c>
/// pipeline. Defaults live here.</summary>
public sealed class StemSeparationOptions
{
    /// <summary>How long the one-off CUDA probe may run before demucs falls back to the CPU. The result is
    /// cached, so this is paid at most once per launch.</summary>
    public TimeSpan CudaProbeTimeout { get; set; } = TimeSpan.FromSeconds(60);
}
