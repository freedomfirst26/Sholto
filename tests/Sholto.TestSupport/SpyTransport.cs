using Sholto.App.Audio;

namespace Sholto.TestSupport;

/// <summary>A transport that counts the calls it gets and flips the scripted loading's play flag the way a
/// real deck would.</summary>
internal sealed class SpyTransport(ScriptedLoading loading) : ITransportControl
{
    private readonly ScriptedLoading _loading = loading;

    public int TogglePlayCalls { get; private set; }

    public void Play() => _loading.IsPlaying = true;

    public void Pause() => _loading.IsPlaying = false;

    public void TogglePlay()
    {
        TogglePlayCalls++;
        _loading.IsPlaying = !_loading.IsPlaying;
    }

    public void SeekRelative(double seconds)
    {
    }

    /// <summary>The fraction of the last seek, or null if none.</summary>
    public double? LastSeekFraction { get; private set; }

    public void SeekToFraction(double fraction) => LastSeekFraction = fraction;
}
