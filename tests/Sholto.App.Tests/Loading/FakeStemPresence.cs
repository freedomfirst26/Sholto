using Sholto.App.Analysis.Stems;

namespace Sholto.App.Tests;

/// <summary>A stem cache that holds exactly the files it is told about.</summary>
internal sealed class FakeStemPresence(params string[] cached) : IStemPresence
{
    private readonly HashSet<string> _cached = [.. cached];

    public bool Contains(string filePath) => _cached.Contains(filePath);

    public StemPaths? TryGet(string filePath) => _cached.Contains(filePath) ? new StemPaths("/cache" + filePath) : null;
}
