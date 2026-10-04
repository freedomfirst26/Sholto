using Sholto.App.Lifecycle;

namespace Sholto.App.Tests;

/// <summary>Records what the lifecycle asked the audio engine to do.</summary>
internal sealed class RecordingAudioOutput : IAudioOutput
{
    public bool Started { get; private set; }
    public string? StartedOn { get; private set; }
    public string? SwitchedTo { get; private set; }
    public bool Stopped { get; private set; }

    public Task StartAsync(string? deviceName)
    {
        Started = true;
        StartedOn = deviceName;
        return Task.CompletedTask;
    }

    public Task SwitchAsync(string deviceName)
    {
        SwitchedTo = deviceName;
        return Task.CompletedTask;
    }

    public void Stop() => Stopped = true;
}
