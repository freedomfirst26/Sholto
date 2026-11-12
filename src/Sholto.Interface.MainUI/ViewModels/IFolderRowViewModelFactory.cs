namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Builds the two rows of the Folders panel.</summary>
public interface IFolderRowViewModelFactory
{
    /// <summary>The music folder row; <c>SHOLTO_MUSIC_DIR</c> fixes it.</summary>
    FolderRowViewModel Music();

    /// <summary>The analysis folder row (where stems are written); <c>SHOLTO_CACHE_ROOT</c> fixes it.</summary>
    FolderRowViewModel Analysis();
}
