using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Data;
using Sholto.Interface.MainUI.Models;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The tag editor: a track's tags as chips, a typed input with suggestions. It reads through
/// queries (a track's tags, suggestions) and edits through commands (<see cref="AddTagToTrack"/>,
/// <see cref="RemoveTagFromTrack"/>); the outcome of an add comes back as <see cref="TagAddAttempted"/>,
/// which is what adds the chip and sets the status line.</summary>
public sealed class TagEditorViewModel : INotifyPropertyChanged, IEventHandler<TagAddAttempted>
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? RequestClose;

    private readonly IQueryAsker _asker;
    private readonly ICommandSender _sender;
    private readonly ITagRecency _recency;

    public TagEditorViewModel(
        IQueryAsker asker, ICommandSender sender, IEventSubscriber subscriber, ITagRecency recency)
    {
        _asker = asker;
        _sender = sender;
        _recency = recency;
        subscriber.Subscribe<TagAddAttempted>(this);
    }

    public Guid TrackId { get; private set; }
    public string TrackArtist { get; private set; } = "";
    public string TrackTitle  { get; private set; } = "";
    public string Title => $"Tags — {TrackArtist} — {TrackTitle}";

    public ObservableCollection<string> Chips { get; } = new();
    public ObservableCollection<string> Suggestions { get; } = new();

    private string _input = "";
    public string Input
    {
        get => _input;
        set
        {
            if (_input == value) return;
            _input = value;
            Notify();
            _ = RefreshSuggestionsAsync();
        }
    }

    private int _suggestionIndex = -1;
    public int SuggestionIndex
    {
        get => _suggestionIndex;
        set { if (_suggestionIndex == value) return; _suggestionIndex = value; Notify(); }
    }

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        private set { if (_statusMessage == value) return; _statusMessage = value; Notify(); }
    }

    public async Task OpenForAsync(Guid trackId, string artist, string title)
    {
        TrackId = trackId;
        TrackArtist = artist;
        TrackTitle = title;
        Notify(nameof(TrackArtist));
        Notify(nameof(TrackTitle));
        Notify(nameof(Title));

        Chips.Clear();
        // A failed query (no handler) answers null: show no chips rather than throw.
        foreach (var t in await _asker.AskAsync<GetTrackTags, IReadOnlyList<string>>(new GetTrackTags(trackId)) ?? [])
            Chips.Add(t);

        _input = "";
        Notify(nameof(Input));
        StatusMessage = null;
        await RefreshSuggestionsAsync();
    }

    /// <summary>Tag the track with the highlighted suggestion, or what was typed. The input clears at once;
    /// the chip and any status line follow when the App reports the outcome.</summary>
    public Task CommitAsync()
    {
        var pending = _suggestionIndex >= 0 && _suggestionIndex < Suggestions.Count
            ? Suggestions[_suggestionIndex]
            : _input;
        if (string.IsNullOrWhiteSpace(pending)) { StatusMessage = null; return Task.CompletedTask; }

        _sender.Send(new AddTagToTrack(TrackId, pending, new Origin(InterfaceIds.MainUI, "tag-editor", "commit")));
        Input = "";
        SuggestionIndex = -1;
        return Task.CompletedTask;
    }

    /// <summary>The App's answer to a tag add. Only this editor's track is of interest.</summary>
    public void Handle(in TagAddAttempted e)
    {
        if (e.TrackId != TrackId) return;
        switch (e.Outcome)
        {
            case TagAddOutcome.Added:
                Chips.Add(e.StoredName!);
                _recency.MarkUsed(e.StoredName);
                StatusMessage = null;
                _ = RefreshSuggestionsAsync();   // the new chip is no longer a suggestion
                break;
            case TagAddOutcome.AlreadyPresent:
                // Still a deliberate selection, so it counts as "recently used".
                _recency.MarkUsed(e.StoredName);
                StatusMessage = $"'{e.StoredName}' is already tagged.";
                break;
            case TagAddOutcome.RejectedEmpty:
                StatusMessage = null;
                break;
            case TagAddOutcome.RejectedTooLong:
                StatusMessage = $"Tag is too long (max {e.Limit}).";
                break;
            case TagAddOutcome.RejectedLimitReached:
                StatusMessage = $"This track is at the {e.Limit}-tag limit.";
                break;
            case TagAddOutcome.RejectedTrackNotFound:
                StatusMessage = "This track isn't in the library DB yet — rescan and try again.";
                break;
            case TagAddOutcome.Unavailable:
                StatusMessage = "The library database isn't available, so tags can't be saved.";
                break;
        }
    }

    public async Task CommitAndCloseAsync()
    {
        await CommitAsync();
        RequestClose?.Invoke();
    }

    public Task RemoveChipAsync(string name)
    {
        _sender.Send(new RemoveTagFromTrack(TrackId, name, new Origin(InterfaceIds.MainUI, "tag-editor", "remove-chip")));
        Chips.Remove(name);
        return Task.CompletedTask;
    }

    public Task RemoveLastChipAsync()
    {
        if (Chips.Count == 0) return Task.CompletedTask;
        return RemoveChipAsync(Chips[^1]);
    }

    public void Close() => RequestClose?.Invoke();

    public void MoveSuggestion(int delta)
    {
        if (Suggestions.Count == 0) { SuggestionIndex = -1; return; }
        int next = SuggestionIndex + delta;
        if (next < 0) next = Suggestions.Count - 1;
        if (next >= Suggestions.Count) next = 0;
        SuggestionIndex = next;
    }

    private async Task RefreshSuggestionsAsync()
    {
        const int maxSuggestions = 5;
        var prefix = _input.Trim();
        var hits = await _asker.AskAsync<SuggestTags, IReadOnlyList<string>>(new SuggestTags(prefix, 10)) ?? [];

        // Tags selected earlier this session lead the list, newest first. The
        // database order (alphabetical) fills the rest. Recent names are matched
        // against the prefix here because they bypass the database query above.
        var recent = _recency.RecentNames(maxSuggestions)
            .Where(n => prefix.Length == 0 ||
                        n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        var picked = recent.Concat(hits)
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .Where(h => !Chips.Contains(h, StringComparer.OrdinalIgnoreCase))
                           .Take(maxSuggestions).ToList();
        Suggestions.Clear();
        foreach (var h in picked) Suggestions.Add(h);
        SuggestionIndex = picked.Count > 0 ? 0 : -1;
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
