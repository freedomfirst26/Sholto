using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Add-to-crate chooser. Type to filter existing crates; if the typed name
/// isn't already a crate, the top row becomes "Create …". Enter adds the track to the
/// highlighted row and persists. Minimal, keyboard-first.</summary>
public sealed class CratePickerViewModel(IQueryAsker asker, ICommandSender sender, IAppThread appThread) : INotifyPropertyChanged
{
    private readonly IQueryAsker _asker = asker;
    private readonly ICommandSender _sender = sender;
    private readonly IAppThread _appThread = appThread;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? RequestClose;

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
        var hits = await _asker.AskAsync<SearchCrates, IReadOnlyList<CrateRef>>(new SearchCrates(q)) ?? [];
        bool canCreate = q.Length > 0 &&
            !hits.Any(h => string.Equals(h.Name, q, StringComparison.OrdinalIgnoreCase));

        // Back on the app thread (the UI thread) before touching the bound collection.
        _appThread.Post(() =>
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

    /// <summary>Add the track to the highlighted crate (creating it if the highlight is the "create" row),
    /// then close. The App does the work and reports <see cref="TrackAddedToCrate"/>, which is what raises the
    /// toast.</summary>
    public Task CommitAsync()
    {
        if (Row is null || SelectedIndex < 0 || SelectedIndex >= Options.Count) return Task.CompletedTask;
        var opt = Options[SelectedIndex];

        var origin = new Origin(InterfaceIds.MainUI, "crate-picker", "commit");
        if (opt.IsCreate)
        {
            var crateName = _query.Trim();
            if (crateName.Length == 0) return Task.CompletedTask;
            _sender.Send(new AddTrackToCrate(Row.TrackId, 0, crateName, true, origin));
        }
        else
        {
            _sender.Send(new AddTrackToCrate(Row.TrackId, opt.CrateId, opt.Display, false, origin));
        }

        RequestClose?.Invoke();
        return Task.CompletedTask;
    }

    public void Close() => RequestClose?.Invoke();

    private void Notify([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
