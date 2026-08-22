using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Storage;

namespace Sholto.App.ViewModels;

public sealed class TagEditorViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? RequestClose;

    private readonly TagService _service;

    public TagEditorViewModel(TagService service)
    {
        _service = service;
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
        foreach (var t in await _service.GetTagsForTrackAsync(trackId, default))
            Chips.Add(t);

        _input = "";
        Notify(nameof(Input));
        StatusMessage = null;
        await RefreshSuggestionsAsync();
    }

    public async Task CommitAsync()
    {
        var pending = _suggestionIndex >= 0 && _suggestionIndex < Suggestions.Count
            ? Suggestions[_suggestionIndex]
            : _input;
        if (string.IsNullOrWhiteSpace(pending)) { StatusMessage = null; return; }

        var result = await _service.AddTagAsync(TrackId, pending, default);
        switch (result.Outcome)
        {
            case AddTagOutcome.Added:
                Chips.Add(result.StoredName!);
                StatusMessage = null;
                break;
            case AddTagOutcome.AlreadyPresent:
                StatusMessage = $"'{result.StoredName}' is already tagged.";
                break;
            case AddTagOutcome.RejectedEmpty:
                StatusMessage = null;
                break;
            case AddTagOutcome.RejectedTooLong:
                StatusMessage = $"Tag is too long (max {TagNameNormalizer.MaxLength}).";
                break;
            case AddTagOutcome.RejectedLimitReached:
                StatusMessage = $"This track is at the {TagService.MaxTagsPerTrack}-tag limit.";
                break;
            case AddTagOutcome.RejectedTrackNotFound:
                StatusMessage = "This track isn't in the library DB yet — rescan and try again.";
                break;
        }
        Input = "";
        SuggestionIndex = -1;
    }

    public async Task CommitAndCloseAsync()
    {
        await CommitAsync();
        RequestClose?.Invoke();
    }

    public async Task RemoveChipAsync(string name)
    {
        await _service.RemoveTagAsync(TrackId, name, default);
        Chips.Remove(name);
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
        var prefix = _input.Trim();
        var hits = await _service.AutocompleteAsync(prefix, 10, default);
        var picked = hits.Where(h => !Chips.Contains(h, StringComparer.OrdinalIgnoreCase)).ToList();
        Suggestions.Clear();
        foreach (var h in picked) Suggestions.Add(h);
        SuggestionIndex = picked.Count > 0 ? 0 : -1;
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
