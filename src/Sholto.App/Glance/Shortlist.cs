using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Glance;

/// <inheritdoc cref="IShortlist"/>
public sealed class Shortlist : IShortlist
{
    private readonly ILibrarySession _library;
    private readonly IEventPublisher _publisher;
    private readonly List<string> _paths = [];

    public Shortlist(ILibrarySession library, IEventPublisher publisher)
    {
        _library = library;
        _publisher = publisher;
        _library.RowUpdated += OnRowUpdated;
        _library.RowsChanged += _ => Announce();
        Announce();
    }

    public IReadOnlyList<string> Paths => _paths;

    public event Action? Changed;

    public void Handle(in ToggleShortlist command)
    {
        if (!_paths.Remove(command.FilePath)) _paths.Add(command.FilePath);
        Announce();
        Changed?.Invoke();
    }

    public void Restore(IReadOnlyList<string> paths)
    {
        var merged = new List<string>();
        foreach (var path in paths.Concat(_paths))
            if (!merged.Contains(path)) merged.Add(path);
        _paths.Clear();
        _paths.AddRange(merged);
        Announce();
    }

    // A listed track's summary changed (BPM landed, played...): the announced snapshot is stale.
    private void OnRowUpdated(TrackSummary row)
    {
        if (_paths.Contains(row.FilePath)) Announce();
    }

    private void Announce()
    {
        var tracks = new List<TrackSummary>(_paths.Count);
        foreach (var path in _paths)
            if (_library.SummaryFor(path) is { } summary) tracks.Add(summary);
        _publisher.Publish(new ShortlistChanged(tracks));
    }
}
