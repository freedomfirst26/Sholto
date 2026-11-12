namespace Sholto.App.Analysis.Stems;

/// <summary>Pure filesystem queries about whether a track's stems are already cached.</summary>
public interface IStemPresence
{
    StemPaths? TryGet(string filePath);
}
