using Sholto.Analysis.Processing;

namespace Sholto.Analysis;

/// <summary>
/// Owns the on-disk layout for demucs' cached stem output: where a track's stems
/// live (<paramref name="workspaceFor"/>, a pure function of the file path — the
/// composition root's cache-directory policy, handed in rather than computed here)
/// combined with demucs' own output layout inside that directory
/// (<see cref="StemPaths.PathsIn"/>). <c>DemucsStemAnalysisStep</c> uses the
/// same <paramref name="workspaceFor"/> function to know where to tell demucs to
/// write — the two agree on a location because they're handed the same function by
/// the composition root, not because either reaches into the other.
///
/// Pure filesystem queries only — never runs demucs. This is what the UI asks at
/// startup (once per library row) instead of the analysis step, so checking every
/// row doesn't trigger hundreds of 30-180 s demucs runs. Also what
/// <see cref="CachingStemAnalysisStep"/> asks on its hot path.
/// </summary>
public sealed class DemucsStemPresence
{
    private readonly Func<string, string> _workspaceFor;

    public DemucsStemPresence(Func<string, string> workspaceFor) => _workspaceFor = workspaceFor;

    private StemPaths PathsFor(string filePath) => StemPaths.PathsIn(_workspaceFor(filePath));

    public bool Contains(string filePath) => PathsFor(filePath).All.All(File.Exists);

    public StemPaths? TryGet(string filePath)
    {
        var paths = PathsFor(filePath);
        return paths.All.All(File.Exists) ? paths : null;
    }
}
