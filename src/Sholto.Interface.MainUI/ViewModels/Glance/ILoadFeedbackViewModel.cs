using System.ComponentModel;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>The replace-a-playing-deck warning, and the undo command.</summary>
public interface ILoadFeedbackViewModel : INotifyPropertyChanged
{
    bool HasWarning { get; }

    /// <summary>Deck 2 is playing · "Title", −2:30 left. Press ⇧2 or LOAD 2 again within 3 s to replace it.</summary>
    string WarningText { get; }

    /// <summary>Undo the last load.</summary>
    void Undo();
}
