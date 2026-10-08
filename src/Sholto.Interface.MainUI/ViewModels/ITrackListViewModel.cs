using System.ComponentModel;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The Track List as the main view shows it: the source chips, the count, the empty state and the
/// drag-to-reorder gesture. It reads <c>TrackListChanged</c> and sends commands; the App owns the list.</summary>
public interface ITrackListViewModel : INotifyPropertyChanged
{
    /// <summary>The sources in the list, in load order.</summary>
    IReadOnlyList<TrackListSourceChip> Sources { get; }

    /// <summary>Total songs in the list.</summary>
    int Count { get; }

    /// <summary>"67 songs" (or "1 song").</summary>
    string CountText { get; }

    /// <summary>True when the list holds no songs, so the strip shows its hint.</summary>
    bool IsEmpty { get; }

    /// <summary>Remove the songs only the chip's source brought.</summary>
    void RemoveSource(string sourceKey);

    /// <summary>Remove the song at <paramref name="path"/>; nothing is sent when it is null (no highlighted row).</summary>
    void RemoveSong(string? path);

    /// <summary>True while a row is being dragged.</summary>
    bool IsDragging { get; }

    /// <summary>The gap (0 = above the first row, N = below the last) the drop line sits in while dragging.</summary>
    int DropGap { get; }

    /// <summary>Start dragging the row at <paramref name="fromIndex"/>.</summary>
    void BeginDrag(string path, int fromIndex, int rowCount);

    /// <summary>The pointer is over gap <paramref name="gap"/>, clamped to the rows.</summary>
    void UpdateDrag(int gap);

    /// <summary>Drop: send the move when the row lands somewhere new. Returns whether a command was sent.</summary>
    bool CompleteDrag();

    /// <summary>Abandon the drag without sending anything.</summary>
    void CancelDrag();
}
