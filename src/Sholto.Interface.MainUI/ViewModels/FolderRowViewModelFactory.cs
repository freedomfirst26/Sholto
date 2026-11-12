namespace Sholto.Interface.MainUI.ViewModels;

/// <inheritdoc />
public sealed class FolderRowViewModelFactory : IFolderRowViewModelFactory
{
    public FolderRowViewModel Music() => new("Music folder", "Set by SHOLTO_MUSIC_DIR");

    public FolderRowViewModel Analysis() => new("Analysis folder", "Set by SHOLTO_CACHE_ROOT");
}
