namespace Sholto.Interface.MainUI;

/// <summary>The Folders panel's settings, supplied via the standard <c>IOptions&lt;FoldersPanelOptions&gt;</c> pipeline
/// like <see cref="DeckViewOptions"/>. Defaults live here.</summary>
public sealed class FoldersPanelOptions
{
    /// <summary>About how many bytes of stems one track takes, for the size hint under the analysis folder.</summary>
    public long StemBytesPerTrackEstimate { get; set; } = 175L * 1024 * 1024;
}
