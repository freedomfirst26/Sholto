using System.Collections.ObjectModel;
using System.ComponentModel;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The in-window "Choose Audio Output" picker: <see cref="AskAsync"/> opens it and the task
/// completes with the chosen device name, or null if the user cancelled.</summary>
public interface IOutputPickerViewModel : INotifyPropertyChanged
{
    event Action? Opened;
    event Action? RequestClose;
    ObservableCollection<OutputDeviceRow> Devices { get; }
    string Headline { get; }
    string Subtext { get; }
    int SelectedIndex { get; set; }
    Task<string?> AskAsync(IReadOnlyList<OutputDeviceChoice> devices, string? currentName);
    void Move(int delta);
    void Commit();
    void Cancel();
}
