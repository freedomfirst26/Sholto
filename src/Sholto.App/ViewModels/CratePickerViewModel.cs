using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Storage;

namespace Sholto.App.ViewModels;

/// <summary>One row in the crate picker: either an existing crate, or the
/// "create '<query>'" affordance at the top.</summary>
public sealed record CratePickerOption(bool IsCreate, string Display, int CrateId, int TrackCount);

/// <summary>Add-to-crate chooser. Type to filter existing crates; if the typed name
/// isn't already a crate, the top row becomes "Create …". Enter adds the track to the
/// highlighted row and persists. Minimal, keyboard-first.</summary>
public sealed class CratePickerViewModel : INotifyPropertyChanged
{
    private readonly CrateService _crates;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? RequestClose;
    /// <summary>Raised after a successful add: (crate name, track title) for a toast.</summary>
    public event Action<string, string>? Added;

    public CratePickerViewModel(CrateService crates) => _crates = crates;

    public TrackRow? Row { get; private set; }
    public string Title => Row is null ? "Add to crate" : $"{Row.Artist} — {Row.Title}";

    /// <summary>The combined option list: optional "create" row first, then matches.</summary>
    public ObservableCollection<CratePickerOption> Options { get; } = new();

    private string _query = "";
    public string Query
    {
        get => _query;
        set { if (_query == value) return; _query = value; Notify(); _ = RefreshAsync(); }
    }

    private int _selectedIndex;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set { if (_selectedIndex == value) return; _selectedIndex = value; Notify(); }
    }

    public async Task OpenAsync(TrackRow row)
    {
        Row = row;
        _query = "";
        Notify(nameof(Row));
        Notify(nameof(Title));
        Notify(nameof(Query));
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var q = _query.Trim();
        var hits = await _crates.SearchAsync(q);
        bool canCreate = q.Length > 0 &&
            !hits.Any(h => string.Equals(h.Name, q, StringComparison.OrdinalIgnoreCase));

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            Options.Clear();
            if (canCreate) Options.Add(new CratePickerOption(true, $"➕  Create “{q}”", 0, 0));
            foreach (var h in hits.Take(5)) Options.Add(new CratePickerOption(false, h.Name, h.Id, h.TrackCount));
            SelectedIndex = 0;
            Notify(nameof(Options));
        });
    }

    public void Move(int delta)
    {
        if (Options.Count == 0) return;
        SelectedIndex = (SelectedIndex + delta + Options.Count) % Options.Count;
    }

    /// <summary>Add the track to the highlighted crate (creating it if the highlight is
    /// the "create" row). Persists, then closes.</summary>
    public async Task CommitAsync()
    {
        if (Row is null || SelectedIndex < 0 || SelectedIndex >= Options.Count) return;
        var opt = Options[SelectedIndex];

        int crateId;
        string crateName;
        if (opt.IsCreate)
        {
            crateName = _query.Trim();
            if (crateName.Length == 0) return;
            crateId = await _crates.CreateAsync(crateName);
        }
        else
        {
            crateName = opt.Display;
            crateId = opt.CrateId;
        }

        await _crates.AddTrackAsync(crateId, Row.TrackId);
        Added?.Invoke(crateName, Row.Title);
        RequestClose?.Invoke();
    }

    public void Close() => RequestClose?.Invoke();

    private void Notify([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
