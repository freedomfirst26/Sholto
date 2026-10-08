namespace Sholto.App.Lifecycle;

/// <summary>The Track List as it is saved: the songs in manual order and the sources that brought them.</summary>
public sealed record SavedTrackList(IReadOnlyList<SavedTrackListEntry> Entries, IReadOnlyList<SavedTrackListSource> Sources);
