using Sholto.App.Analysis.Stores;

namespace Sholto.TestSupport;

/// <summary>Holds the overrides it is given and records every one written.</summary>
internal sealed class FakeTempoMultiplierStore(IReadOnlyDictionary<string, double>? stored = null) : ITempoMultiplierStore
{
    private readonly IReadOnlyDictionary<string, double> _stored = stored ?? new Dictionary<string, double>();

    public List<(string Path, double Multiplier)> Puts { get; } = [];

    public Task<IReadOnlyDictionary<string, double>> GetAllAsync() => Task.FromResult(_stored);

    public Task PutAsync(string path, double multiplier)
    {
        Puts.Add((path, multiplier));
        return Task.CompletedTask;
    }
}
