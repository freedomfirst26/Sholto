using Sholto.App.Decks;
using Sholto.App.Library.Markers;

namespace Sholto.App.Tests;

/// <summary>Records which decks had their marker overlay refreshed.</summary>
internal sealed class RecordingDeckMarkers : IDeckMarkers
{
    public List<int> Refreshed { get; } = [];

    public event Action<int, double>? MarkerAdded { add { } remove { } }

    public void Attach(IMarkerService markers) { }

    public Task AddAsync(int deck) => Task.CompletedTask;

    public Task RefreshAsync(int deck)
    {
        Refreshed.Add(deck);
        return Task.CompletedTask;
    }
}
