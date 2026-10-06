namespace Sholto.App.Analysis.Stems;

/// <summary>An <see cref="IStemDevice"/> that is already decided: for hosts and tests that never probe.</summary>
public sealed class FixedStemDevice(StemDevice device) : IStemDevice
{
    private readonly StemDevice _device = device;

    public Task<StemDevice> ResolveAsync(CancellationToken ct = default) => Task.FromResult(_device);
}
