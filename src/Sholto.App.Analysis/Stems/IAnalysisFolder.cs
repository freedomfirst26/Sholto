namespace Sholto.App.Analysis.Stems;

/// <summary>Where stems are written: the analysis folder the user chose. It is late-bound, since the
/// choice is only known once startup has asked.</summary>
public interface IAnalysisFolder
{
    /// <summary>The folder in use, or null before <see cref="Use"/>. While null, stem work fails closed.
    /// Safe to read from any thread.</summary>
    string? Root { get; }

    /// <summary>Start using <paramref name="root"/>, replacing any earlier folder.</summary>
    void Use(string root);
}
