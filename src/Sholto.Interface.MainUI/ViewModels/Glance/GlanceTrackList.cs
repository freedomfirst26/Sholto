using Microsoft.Extensions.Options;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

public sealed class GlanceTrackList : IGlanceTrackList
{
    private readonly ICommandSender _sender;
    private readonly ITagRecency _tagRecency;
    private readonly IFrameClock _clock;
    private readonly int _clearArmMs;

    private HashSet<string> _keys = [];
    private HashSet<string> _paths = [];
    private int _count;
    private DateTime? _clearArmedAt;

    public GlanceTrackList(ICommandSender sender, ITagRecency tagRecency, IFrameClock clock,
        IOptions<GlanceViewOptions> options)
    {
        _sender = sender;
        _tagRecency = tagRecency;
        _clock = clock;
        _clearArmMs = options.Value.ClearArmMs;
    }

    public event Action? ClearArmedChanged;

    public int Count => _count;

    public IReadOnlySet<string> Keys => _keys;

    public string? ClearArmedText => _clearArmedAt is null ? null : $"Ctrl Del again: clear {_count}";

    public bool Contains(string path) => _paths.Contains(path);

    public void Apply(TrackListChanged e)
    {
        _count = e.Count;
        _keys = [.. e.Sources.Select(s => s.Key)];
        _paths = [.. e.Paths];
    }

    public void DisarmIfEmpty()
    {
        if (_clearArmedAt is not null && _count == 0) Disarm();
    }

    public GlanceLoadState LoadState(string? highlightedPath, object? railItem) =>
        Loadable(highlightedPath, railItem) switch
        {
            null => GlanceLoadState.Off,
            { InList: true } => GlanceLoadState.InList,
            _ => GlanceLoadState.Ready,
        };

    public int LoadCount(string? highlightedPath, object? railItem) =>
        Loadable(highlightedPath, railItem) is { InList: false, Count: var n } ? n : 0;

    public void Load(string? highlightedPath, object? railItem)
    {
        if (Loadable(highlightedPath, railItem) is not { InList: false } item) return;
        if (item.Path is { } path)
            _sender.Send(new LoadSongToTrackList(path, Ui("load-track-list")));
        else if (item.IsTag)
        {
            _tagRecency.MarkUsed(item.Name!);
            _sender.Send(new LoadTagToTrackList(item.Name!, Ui("load-track-list")));
        }
        else
            _sender.Send(new LoadCrateToTrackList(item.CrateId, item.Name!, Ui("load-track-list")));
    }

    public void Toggle(string path)
    {
        if (_paths.Contains(path)) _sender.Send(new RemoveFromTrackList(path, Ui("star")));
        else _sender.Send(new LoadSongToTrackList(path, Ui("star")));
    }

    public void Clear()
    {
        if (_count == 0) return;
        if (_clearArmedAt is { } armed && (_clock.Now - armed).TotalMilliseconds <= _clearArmMs)
        {
            Disarm();
            _sender.Send(new ClearTrackList(Ui("clear-track-list")));
            return;
        }
        SetClearArmed(_clock.Now);
    }

    public void Disarm() => SetClearArmed(null);

    public void Tick(DateTime now)
    {
        if (_clearArmedAt is { } armed && (now - armed).TotalMilliseconds > _clearArmMs) Disarm();
    }

    private void SetClearArmed(DateTime? at)
    {
        if (_clearArmedAt is null && at is null) return;
        _clearArmedAt = at;
        ClearArmedChanged?.Invoke();
    }

    /// <summary>What Ctrl+L would add: the highlighted song, crate or tag, and whether it is already in the list.</summary>
    private (string? Path, int CrateId, string? Name, bool IsTag, bool InList, int Count)? Loadable(
        string? highlightedPath, object? railItem)
    {
        if (highlightedPath is { } path)
            return (path, 0, null, false, _paths.Contains(path), 0);
        return railItem switch
        {
            GlanceRailCrate c => (null, c.Id, c.Name, false, c.IsInTrackList, c.Count),
            GlanceRailTag t => (null, 0, t.Name, true, t.IsInTrackList, t.Count),
            _ => null,
        };
    }

    private Origin Ui(string gesture) => new(InterfaceIds.MainUI, "glance", gesture);
}
