namespace Sholto.Analysis;

/// <summary>
/// Shared path-encoding helper for the on-disk analysis caches (<see cref="DemucsStemAnalysisStep"/>'s
/// stems).
/// </summary>
public static class AnalysisCachePaths
{
    /// <summary>Encode an absolute source path into one safe, stable directory
    /// segment. Just the path with slashes / spaces / invalid chars replaced.
    /// Stable across process restarts (unlike string.GetHashCode, which is
    /// randomised per process in modern .NET). Rename a track and it'll be
    /// re-analysed — acceptable.
    ///
    /// This encoding is a real contract: every cache directory already written to
    /// disk was named by it, so changing it orphans every existing user's cached
    /// analysis. Preserve it exactly.</summary>
    public static string DirNameFor(string filePath)
    {
        var full = Path.GetFullPath(filePath);
        var bad = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(full.Length);
        foreach (var c in full)
            sb.Append(c == '/' || c == '\\' || c == ' ' || bad.Contains(c) ? '_' : c);
        // Trim a leading underscore from the root '/' on Linux for cosmetics.
        return sb.ToString().TrimStart('_');
    }
}
