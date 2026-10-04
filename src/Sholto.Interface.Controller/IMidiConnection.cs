namespace Sholto.Interface.Controller;

/// <summary>A live MIDI connection to a controller: raises its events, accepts
/// raw bytes out, and exposes the active device mapping.</summary>
public interface IMidiConnection : IDisposable
{
    event Action<ControllerEvent>? EventReceived;

    /// <summary>Raised each time the controller (re)connects.</summary>
    event Action? Connected;

    /// <summary>Raised when a live controller drops out.</summary>
    event Action? ConnectionLost;

    /// <summary>True while a device is currently open.</summary>
    bool IsConnected { get; }

    /// <summary>The active device mapping, or null until <see cref="Connect"/> succeeds.</summary>
    IControllerMapping? Mapping { get; }

    /// <summary>Write raw MIDI bytes out to the controller.</summary>
    void Send(byte[] bytes);

    /// <summary>Start keeping a controller connected; returns whether the first attempt connected.</summary>
    bool Connect();
}
