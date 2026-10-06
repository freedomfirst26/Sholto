using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Input;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Modal;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Add-to-crate chooser. Type to filter existing crates; if the typed name
/// isn't already a crate, the top row becomes "Create …". Enter adds the track to the
/// highlighted row and persists. Minimal, keyboard-first.</summary>
public sealed class CratePickerViewModel(IQueryAsker asker, ICommandSender sender, IAppThread appThread) : IModalContent
{
    private readonly IQueryAsker _asker = asker;
    private readonly ICommandSender _sender = sender;
    private readonly IAppThread _appThread = appThread;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? RequestClose;

    private bool _isOpen;
    public bool IsOpen
    {
        get => _isOpen;
        private set { if (_isOpen == value) return; _isOpen = value; Notify(); }
    }

    public string Eyebrow => "📦  ADD TO CRATE";
    public ModalTone Tone => ModalTone.Accent;
    public string? Subtitle => null;
    public string KeyHint => "↑↓ choose  ·  Enter add  ·  Esc cancel";
    public ModalWidth Width => ModalWidth.Narrow;
    public ModalScrimClick ScrimClick => ModalScrimClick.Dismisses;
    public bool CapturesText => true;
    public bool CanGoBack => false;
    public bool CanConfirm => Options.Count > 0;

    /// <summary>The whole button set, in one place. Primary reads "Create" while the highlight is the create row.</summary>
    public ModalButtons Buttons => new("Cancel", null, HighlightIsCreate ? "Create" : "Add");

    private bool HighlightIsCreate => SelectedIndex >= 0 && SelectedIndex < Options.Count && Options[SelectedIndex].IsCreate;

    public void Dismiss() => Close();
    public void Back() { }
    public void Confirm() => _ = CommitAsync();

    public bool HandleKey(Key key, KeyModifiers modifiers)
    {
        switch (key)
        {
            case Key.Up: Move(-1); return true;
            case Key.Down: Move(1); return true;
            default: return false;
        }
    }

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
        set { if (_selectedIndex == value) return; _selectedIndex = value; Notify(); Notify(nameof(Buttons)); }
    }

    public async Task OpenAsync(TrackRow row)
    {
        Row = row;
        _query = "";
        Notify(nameof(Row));
        Notify(nameof(Title));
        Notify(nameof(Query));
        IsOpen = true;
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
            Notify(nameof(CanConfirm));
            Notify(nameof(Buttons));
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

        Close();
        return Task.CompletedTask;
    }

    public void Close()
    {
        IsOpen = false;
        RequestClose?.Invoke();
    }

    private void Notify([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
