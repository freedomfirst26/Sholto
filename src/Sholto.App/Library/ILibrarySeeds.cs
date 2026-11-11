using Sholto.App.Analysis.Harmony;

namespace Sholto.App.Library;

/// <summary>Loads facts already known about songs (stored BPMs, multipliers, keys) onto the catalog's rows.</summary>
public interface ILibrarySeeds
{
    void SeedKnownBpms(IReadOnlyDictionary<string, double> bpms);

    void SeedKnownBpmMultipliers(IReadOnlyDictionary<string, double> multipliers);

    /// <summary>Does not refresh the harmony reference; the session does that after.</summary>
    void SeedKnownKeys(IReadOnlyDictionary<string, Key> keys);
}
