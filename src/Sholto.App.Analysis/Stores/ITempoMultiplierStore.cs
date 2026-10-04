namespace Sholto.App.Analysis.Stores;

/// <summary>The user's per-track half/double-time multiplier, keyed by file path.</summary>
public interface ITempoMultiplierStore
{
    /// <summary>Path to multiplier for every track with a stored override. A track with
    /// no override is absent (its multiplier is 1.0).</summary>
    Task<IReadOnlyDictionary<string, double>> GetAllAsync();

    /// <summary>Store the multiplier for <paramref name="path"/>. A multiplier within
    /// 0.0001 of 1.0 removes the override. A path unknown to the catalog is silently ignored.</summary>
    Task PutAsync(string path, double multiplier);
}
