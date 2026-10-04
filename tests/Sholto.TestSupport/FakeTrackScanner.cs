using Sholto.App.Library;

namespace Sholto.TestSupport;

/// <summary>A scanner that "finds" the tracks it was given, whatever the folder.</summary>
internal sealed class FakeTrackScanner(IReadOnlyList<Track> tracks) : ITrackScanner
{
    private readonly IReadOnlyList<Track> _tracks = tracks;

    public Task<IReadOnlyList<Track>> ScanAsync(string directory, CancellationToken cancellationToken = default) =>
        Task.FromResult(_tracks);
}
