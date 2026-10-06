namespace Sholto.App.Analysis.Stems;

/// <summary>Which device demucs will run on. Confirmed once, then cached: callers get the same
/// answer for the life of the process and never pay for a second probe.</summary>
public interface IStemDevice
{
    /// <summary>The confirmed device. Resolves to <see cref="StemDevice.Cpu"/> when CUDA cannot be confirmed.</summary>
    Task<StemDevice> ResolveAsync(CancellationToken ct = default);
}
