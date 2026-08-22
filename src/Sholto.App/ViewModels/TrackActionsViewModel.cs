using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sholto.App.ViewModels;

/// <summary>What pressing Enter on a library row can do. Kept tiny and keyboard-
/// first — the whole point is "one key, a short menu, get out of the way".</summary>
public enum TrackActionKind { Tag, AddToCrate, LoadDeck1, LoadDeck2 }

public sealed record TrackActionItem(string Icon, string Label, TrackActionKind Kind, string Key);

/// <summary>Drives the Enter-mode action menu for a highlighted track. Replaces the
/// old <c>T</c>-to-tag shortcut with a small chooser: Tag / Add to crate / Load.</summary>
public sealed class TrackActionsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? RequestClose;
    public event Action<TrackActionKind>? Invoked;

    public TrackRow? Row { get; private set; }
    public string Title => Row is null ? "" : $"{Row.Artist} — {Row.Title}";

    public ObservableCollection<TrackActionItem> Actions { get; } = new()
    {
        new("①", "Load to Deck 1", TrackActionKind.LoadDeck1, "1"),
        new("②", "Load to Deck 2", TrackActionKind.LoadDeck2, "2"),
        new("📦", "Add to crate",   TrackActionKind.AddToCrate, "C"),
        new("🏷", "Tag",            TrackActionKind.Tag, "T"),
    };

    private int _selectedIndex;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set { if (_selectedIndex == value) return; _selectedIndex = value; Notify(); }
    }

    public void Open(TrackRow row)
    {
        Row = row;
        SelectedIndex = 0;
        Notify(nameof(Row));
        Notify(nameof(Title));
    }

    public void Move(int delta)
    {
        if (Actions.Count == 0) return;
        SelectedIndex = (SelectedIndex + delta + Actions.Count) % Actions.Count;
    }

    /// <summary>Fire the highlighted action.</summary>
    public void Commit()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Actions.Count) return;
        Invoked?.Invoke(Actions[SelectedIndex].Kind);
    }

    /// <summary>Fire a specific action (e.g. click).</summary>
    public void Invoke(TrackActionKind kind) => Invoked?.Invoke(kind);

    public void Close() => RequestClose?.Invoke();

    private void Notify([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
