using Sholto.App.Library;
using Sholto.App.Loading;
using Sholto.Data;

namespace Sholto.App.Glance;

/// <inheritdoc cref="IRecentLoads"/>
public sealed class RecentLoads : IRecentLoads
{
    private readonly ILibrarySession _library;
    private readonly IEventPublisher _publisher;
    private readonly List<Track> _loads = [];

    public RecentLoads(ITrackLoader loader, ILibrarySession library, IEventPublisher publisher)
    {
        _library = library;
        _publisher = publisher;
        loader.Accepted += OnAccepted;
        _library.RowUpdated += OnRowUpdated;
        Announce();
    }

    public int Capacity => 6;

    public IReadOnlyList<string> Paths => _loads.Select(t => t.FilePath).ToList();

    private void OnAccepted(int deck, Track track)
    {
        _loads.RemoveAll(t => t.FilePath == track.FilePath);
        _loads.Insert(0, track);
        if (_loads.Count > Capacity) _loads.RemoveRange(Capacity, _loads.Count - Capacity);
        Announce();
    }

    // A listed track's summary changed (played, BPM landed...): the announced snapshot is stale.
    private void OnRowUpdated(TrackSummary row)
    {
        if (_loads.Any(t => t.FilePath == row.FilePath)) Announce();
    }

    private void Announce()
    {
        var tracks = new List<TrackSummary>(_loads.Count);
        foreach (var track in _loads)
            tracks.Add(_library.SummaryFor(track.FilePath) ?? track.ToSummary());
        _publisher.Publish(new RecentLoadsChanged(tracks));
    }
}
