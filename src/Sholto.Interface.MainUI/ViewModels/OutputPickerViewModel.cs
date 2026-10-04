using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Drives the in-window "Choose Audio Output" picker. <see cref="AskAsync"/> opens it and the
/// returned task completes with the chosen device name, or null if the user cancelled.</summary>
public sealed class OutputPickerViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Opened;
    public event Action? RequestClose;

    public ObservableCollection<OutputDeviceRow> Devices { get; } = new();

    private string _headline = "";
    public string Headline
    {
        get => _headline;
        private set { if (_headline == value) return; _headline = value; Notify(); }
    }

    private string _subtext = "";
    public string Subtext
    {
        get => _subtext;
        private set { if (_subtext == value) return; _subtext = value; Notify(); }
    }

    private int _selectedIndex;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set { if (_selectedIndex == value) return; _selectedIndex = value; Notify(); }
    }

    private TaskCompletionSource<string?>? _pending;

    /// <summary>Show the picker. Preselects the current device, else the system default, else the first.
    /// An empty list returns null at once without opening.</summary>
    public Task<string?> AskAsync(IReadOnlyList<OutputDeviceChoice> devices, string? currentName)
    {
        if (devices.Count == 0) return Task.FromResult<string?>(null);

        // A new question supersedes one still open.
        _pending?.TrySetResult(null);

        Devices.Clear();
        foreach (var d in devices)
            Devices.Add(new OutputDeviceRow(d.Name, d.IsDefault, d.Name == currentName));

        bool known = currentName is not null && Devices.Any(r => r.IsCurrent);
        if (currentName is null)
        {
            Headline = "Where should the master mix play?";
            Subtext = "Pick your speakers or audio interface. You can change this later under Settings → Output device…";
        }
        else if (!known)
        {
            Headline = $"“{currentName}” isn't connected";
            Subtext = "Pick another output for the master mix.";
        }
        else
        {
            Headline = "Output device";
            Subtext = "Where the master mix plays.";
        }

        int index = Devices.ToList().FindIndex(r => r.IsCurrent);
        if (index < 0) index = Devices.ToList().FindIndex(r => r.IsDefault);
        SelectedIndex = index < 0 ? 0 : index;

        _pending = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = _pending.Task;
        Opened?.Invoke();
        return task;
    }

    /// <summary>Move the highlight, stopping at the first and last row.</summary>
    public void Move(int delta)
    {
        if (Devices.Count == 0) return;
        SelectedIndex = Math.Clamp(SelectedIndex + delta, 0, Devices.Count - 1);
    }

    /// <summary>Choose the highlighted device and close.</summary>
    public void Commit()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Devices.Count) return;
        Finish(Devices[SelectedIndex].Name);
    }

    /// <summary>Close without choosing.</summary>
    public void Cancel() => Finish(null);

    private void Finish(string? result)
    {
        var pending = _pending;
        _pending = null;
        RequestClose?.Invoke();
        pending?.TrySetResult(result);
    }

    private void Notify([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
