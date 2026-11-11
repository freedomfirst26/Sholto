using Sholto.App.Analysis.Harmony;

namespace Sholto.App.Library;

/// <summary>See <see cref="ILibrarySeeds"/>.</summary>
public sealed class LibrarySeeds(ILibraryCatalog catalog) : ILibrarySeeds
{
    private readonly ILibraryCatalog _catalog = catalog;

    public void SeedKnownBpms(IReadOnlyDictionary<string, double> bpms)
    {
        var all = _catalog.All;
        for (var i = 0; i < all.Count; i++)
            if (bpms.TryGetValue(all[i].FilePath, out var bpm) && all[i].Bpm != bpm)
                _catalog.Replace(i, all[i] with { Bpm = bpm });
    }

    public void SeedKnownBpmMultipliers(IReadOnlyDictionary<string, double> multipliers)
    {
        var all = _catalog.All;
        for (var i = 0; i < all.Count; i++)
            if (multipliers.TryGetValue(all[i].FilePath, out var m) && all[i].BpmMultiplier != m)
                _catalog.Replace(i, all[i] with { BpmMultiplier = m });
    }

    public void SeedKnownKeys(IReadOnlyDictionary<string, Key> keys)
    {
        var all = _catalog.All;
        for (var i = 0; i < all.Count; i++)
            if (keys.TryGetValue(all[i].FilePath, out var k) && all[i].MusicalKey != k.ToRef())
                _catalog.Replace(i, all[i] with { MusicalKey = k.ToRef() });
    }
}
