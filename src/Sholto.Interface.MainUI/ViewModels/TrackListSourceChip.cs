using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Chips;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>One source chip on the Track List strip: what brought songs in and how many it brought. The chip's ×
/// removes the songs only that source brought (see <see cref="ITrackListViewModel.RemoveSource"/>).</summary>
public sealed class TrackListSourceChip(TrackListSource source, bool isOnlySource = false)
{
    /// <summary>The source's identity, as the remove command wants it.</summary>
    public string Key { get; } = source.Key;

    /// <summary>The look the shared chip control gives this source.</summary>
    public SourceChipKind ChipKind { get; } = source.Kind switch
    {
        TrackListSourceKind.Crate => SourceChipKind.Crate,
        TrackListSourceKind.Tag => SourceChipKind.Tag,
        _ => SourceChipKind.Songs,
    };

    /// <summary>The chip text: the crate's or tag's name as search shows it, loose songs as "N song(s)".</summary>
    public string Label { get; } = source.Kind switch
    {
        TrackListSourceKind.Crate => source.Name,
        TrackListSourceKind.Tag => source.Name.TrimStart('#'),
        _ => $"♪ {source.Count} {(source.Count == 1 ? "song" : "songs")}",
    };

    /// <summary>Songs this source brought.</summary>
    public int Count { get; } = source.Count;

    /// <summary>Whether the count is shown beside the label: not for loose songs (the label carries it) and not
    /// for the only source on the strip (the total beside it would repeat it).</summary>
    public bool ShowCount { get; } = source.Kind != TrackListSourceKind.Songs && !isOnlySource;
}
