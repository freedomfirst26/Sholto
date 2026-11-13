using Sholto.App.Audio;

namespace Sholto.App.Tests;

/// <summary>An <see cref="IDeckLooping"/> that keeps a loop of whole bars of <see cref="SamplesPerBar"/>
/// samples from sample 0, halves and doubles it, and records every call, so the beat-length loop built on
/// top of it can be measured.</summary>
internal sealed class RecordingLooping : IDeckLooping
{
    public const long SamplesPerBar = 192_000;   // 2 s of stereo at 48 kHz: 120 BPM

    public List<string> Calls { get; } = [];
    public LoopRegion? ActiveLoop { get; private set; }
    public double? ActiveLoopBeats { get; private set; }

    public event Action<LoopRegion?>? LoopChanged;

    public void EnableBeatLoop(int bars)
    {
        Calls.Add($"enable {bars}");
        ActiveLoop = ActiveLoop is null ? new LoopRegion(0, bars * SamplesPerBar) : null;
        ActiveLoopBeats = ActiveLoop is null ? null : bars * 4.0;
        LoopChanged?.Invoke(ActiveLoop);
    }

    public void HalveLoop()
    {
        Calls.Add("halve");
        if (ActiveLoop is { } r)
        {
            ActiveLoop = r with { EndSample = r.StartSample + r.LengthSamples / 2 };
            ActiveLoopBeats /= 2;
        }
    }

    public void DoubleLoop()
    {
        Calls.Add("double");
        if (ActiveLoop is { } r)
        {
            ActiveLoop = r with { EndSample = r.StartSample + r.LengthSamples * 2 };
            ActiveLoopBeats *= 2;
        }
    }

    public void ExitLoop()
    {
        Calls.Add("exit");
        ActiveLoop = null;
        ActiveLoopBeats = null;
    }
}
