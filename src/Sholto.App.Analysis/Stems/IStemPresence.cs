namespace Sholto.App.Analysis.Stems;

/// <summary>Pure filesystem queries about whether a track's stems are already cached.</summary>
public interface IStemPresence
{
    bool Contains(string filePath);
    StemPaths? TryGet(string filePath);
}
