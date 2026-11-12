namespace Sholto.App;

/// <summary>The analysis folder (where the demucs stems are written), supplied via the standard
/// <c>IOptions&lt;AnalysisFolderOptions&gt;</c> pipeline like <see cref="LoadOptions"/>. Defaults live here.</summary>
public sealed class AnalysisFolderOptions
{
    /// <summary>The name of the folder suggested next to the music folder.</summary>
    public string FolderName { get; set; } = "Sholto Analysis";

    /// <summary>Bytes that must stay free on the target drive, on top of the stems themselves, before a
    /// move copies anything across drives.</summary>
    public long FreeSpaceMargin { get; set; } = 1L << 30;
}
