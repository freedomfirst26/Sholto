namespace Sholto.App.Analysis.Stems;

/// <summary><see cref="IAnalysisFolder"/> holding the folder in memory. One instance is shared by everything that locates stems.</summary>
public sealed class AnalysisFolder : IAnalysisFolder
{
    private string? _root;

    public string? Root => Volatile.Read(ref _root);

    public void Use(string root) => Volatile.Write(ref _root, root);
}
