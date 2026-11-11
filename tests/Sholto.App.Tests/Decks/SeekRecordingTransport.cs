using Sholto.App.Audio;
using Sholto.App.Library;

namespace Sholto.App.Tests;

/// <summary>A transport that records the relative seeks it gets, moves the scripted playhead by them, and
/// flips the scripted loading's play flag like a real deck.</summary>
internal sealed class SeekRecordingTransport(ScriptedPorts ports) : ITransportControl
{
    private readonly ScriptedPorts _ports = ports;

    public List<double> Seeks { get; } = [];
    public int TogglePlayCalls { get; private set; }

    public void Play() => _ports.ScriptedLoading.IsPlaying = true;
    public void Pause() => _ports.ScriptedLoading.IsPlaying = false;

    public void TogglePlay()
    {
        TogglePlayCalls++;
        _ports.ScriptedLoading.IsPlaying = !_ports.ScriptedLoading.IsPlaying;
    }

    public void SeekRelative(double seconds)
    {
        Seeks.Add(seconds);
        _ports.ScriptedPlayhead.PositionFrames += (long)Math.Round(seconds * AudioFileDecoder.TargetSampleRate);
    }

    public void SeekToFraction(double fraction)
    {
    }
}
