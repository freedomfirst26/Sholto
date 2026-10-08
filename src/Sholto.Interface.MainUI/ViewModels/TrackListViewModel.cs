using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The Track List strip and its reorder drag. It subscribes to <see cref="TrackListChanged"/> (the App
/// replays the current list to a late subscriber) and sends the remove and move commands. Events arrive on the
/// UI thread, so state is set directly.</summary>
public sealed class TrackListViewModel : ITrackListViewModel, IEventHandler<TrackListChanged>
{
    private readonly ICommandSender _sender;
    private IReadOnlyList<TrackListSourceChip> _sources = [];
    private int _count;
    private bool _isDragging;
    private int _dropGap;
    private string _dragPath = "";
    private int _dragFrom;
    private int _dragRows;

    public TrackListViewModel(ICommandSender sender, IEventSubscriber subscriber)
    {
        _sender = sender;
        subscriber.Subscribe<TrackListChanged>(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<TrackListSourceChip> Sources => _sources;
    public int Count => _count;
    public string CountText => _count == 1 ? "1 song" : $"{_count} songs";
    public bool IsEmpty => _count == 0;
    public bool IsDragging => _isDragging;
    public int DropGap => _dropGap;

    public void Handle(in TrackListChanged e)
    {
        var sources = e.Sources;
        var onlyOne = sources.Count == 1;
        _sources = sources.Select(s => new TrackListSourceChip(s, onlyOne)).ToArray();
        _count = e.Count;
        Notify(nameof(Sources));
        Notify(nameof(Count));
        Notify(nameof(CountText));
        Notify(nameof(IsEmpty));
    }

    public void RemoveSource(string sourceKey) =>
        _sender.Send(new RemoveSourceFromTrackList(sourceKey, Ui("chip", "remove")));

    public void RemoveSong(string? path)
    {
        if (path is null) return;
        _sender.Send(new RemoveFromTrackList(path, Ui("library", "delete")));
    }

    public void BeginDrag(string path, int fromIndex, int rowCount)
    {
        _dragPath = path;
        _dragFrom = fromIndex;
        _dragRows = rowCount;
        _dropGap = fromIndex;
        _isDragging = true;
        Notify(nameof(IsDragging));
        Notify(nameof(DropGap));
    }

    public void UpdateDrag(int gap)
    {
        if (!_isDragging) return;
        gap = Math.Clamp(gap, 0, _dragRows);
        if (gap == _dropGap) return;
        _dropGap = gap;
        Notify(nameof(DropGap));
    }

    public bool CompleteDrag()
    {
        if (!_isDragging) return false;
        // A gap below the dragged row's own position shifts by one once the row leaves its place.
        var toIndex = _dropGap > _dragFrom ? _dropGap - 1 : _dropGap;
        var moved = toIndex != _dragFrom;
        if (moved) _sender.Send(new MoveInTrackList(_dragPath, toIndex, Ui("library", "drag")));
        CancelDrag();
        return moved;
    }

    public void CancelDrag()
    {
        if (!_isDragging) return;
        _isDragging = false;
        Notify(nameof(IsDragging));
    }

    private Origin Ui(string control, string gesture) => new(InterfaceIds.MainUI, control, gesture);

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
