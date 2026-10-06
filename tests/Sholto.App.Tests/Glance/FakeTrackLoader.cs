using Sholto.App.Library;
using Sholto.App.Loading;
using Sholto.Data;

namespace Sholto.App.Tests.Glance;

/// <summary>A loader that loads nothing: a test raises <see cref="Accepted"/> by hand.</summary>
internal sealed class FakeTrackLoader : ITrackLoader
{
    public event Action<int, Track>? Accepted;

    public void Raise(int deck, Track track) => Accepted?.Invoke(deck, track);

    public void Handle(in LoadSelectedIntoDeck command)
    {
    }

    public void Handle(in ReanalyzeSelected command)
    {
    }

    public void Handle(in UndoLastLoad command)
    {
    }
}
