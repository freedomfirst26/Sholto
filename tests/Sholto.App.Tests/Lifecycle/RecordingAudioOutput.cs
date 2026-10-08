using MasterRoute = Sholto.App.Audio.MasterRoute;
using Sholto.App.Lifecycle;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>Records what the lifecycle asked the audio engine to do. Each start or switch reports master on
/// the device asked for (on the controller when none), unless <see cref="NextRoute"/> says otherwise.</summary>
internal sealed class RecordingAudioOutput : IAudioOutput
{
    public bool Started { get; private set; }
    public string? StartedOn { get; private set; }
    public string? SwitchedTo { get; private set; }
    public bool Stopped { get; private set; }

    /// <summary>What the next start or switch reports instead of the default, e.g. a failed route.</summary>
    public MasterRoute? NextRoute { get; set; }

    public Task<MasterRoute> StartAsync(string? deviceName)
    {
        Started = true;
        StartedOn = deviceName;
        return Task.FromResult(Route(deviceName));
    }

    public Task<MasterRoute> SwitchAsync(string deviceName)
    {
        SwitchedTo = deviceName;
        return Task.FromResult(Route(deviceName));
    }

    public void Stop() => Stopped = true;

    private MasterRoute Route(string? deviceName)
    {
        var route = NextRoute ?? (deviceName is null
            ? new MasterRoute(AppLifecycleRig.ControllerCard, "no separate speaker chosen")
            : new MasterRoute(deviceName, "routed"));
        NextRoute = null;
        return route;
    }
}
