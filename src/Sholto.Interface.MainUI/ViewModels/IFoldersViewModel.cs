using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Modal;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The Folders panel (first run, upgrade, drive missing, Settings ▸ Folders…): the Music and Analysis
/// folders as two independent rows. Picking one never changes the other, except that the analysis default
/// follows the music folder until the user picks the analysis folder themselves. The App's question is
/// <see cref="FoldersNeeded"/> (shown by <see cref="Show"/>); the answer is sent as <c>ChooseFolders</c> (both
/// paths null when dismissed). After Continue the panel stays open, working, until the App publishes
/// <c>AnalysisFolderChanged</c> or asks again with a problem. While working, dismiss does nothing.</summary>
public interface IFoldersViewModel : IModalContent
{
    /// <summary>Show the question. A question already open is replaced.</summary>
    void Show(FoldersNeeded question);

    /// <summary>The music row, then the analysis row.</summary>
    FolderRowViewModel Music { get; }

    FolderRowViewModel Analysis { get; }

    IReadOnlyList<FolderRowViewModel> Rows { get; }

    /// <summary>The user chose <paramref name="path"/> for <paramref name="row"/> in the OS folder picker. Only that
    /// row changes; the pair is then checked again.</summary>
    void Pick(FolderRowViewModel row, string path);

    /// <summary>Continue was pressed and the App has not answered yet.</summary>
    bool IsWorking { get; }

    /// <summary>The line shown while working: the move progress, or "Applying…".</summary>
    string StatusText { get; }
}
