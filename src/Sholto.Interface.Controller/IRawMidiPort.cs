namespace Sholto.Interface.Controller;

/// <summary>
/// One open MIDI connection to a controller, independent of how the OS exposes it:
/// parsed channel messages in, raw bytes out, and a signal when the device goes away.
/// Opened by an <see cref="IMidiPortOpener"/> and owned by <see cref="MidiManager"/>.
///
/// Contract every implementation keeps:
/// <list type="bullet">
/// <item><see cref="MessageReceived"/> and <see cref="Disconnected"/> may fire on any
/// background thread; the consumer marshals them itself.</item>
/// <item><see cref="Disconnected"/> fires at most once, and only for a real loss of
/// the device (unplug, invalidated handle) — never because of <see cref="IDisposable.Dispose"/>.</item>
/// <item><see cref="Send"/> never throws: a failed write is logged and the bytes dropped.</item>
/// <item><see cref="IDisposable.Dispose"/> is safe to call from a thread-pool thread,
/// including after <see cref="Disconnected"/> has fired.</item>
/// </list>
/// </summary>
public interface IRawMidiPort : IDisposable
{
    /// <summary>A complete channel or system message: status, data1, data2 (0 when the
    /// message has fewer data bytes). Running status is already expanded.</summary>
    event Action<byte, byte, byte>? MessageReceived;

    /// <summary>Fired once when the device goes away mid-session. Not fired on Dispose.</summary>
    event Action? Disconnected;

    /// <summary>Write raw MIDI bytes to the controller (LED updates, device startup
    /// init, etc.). Never throws; a failure is logged and the bytes dropped.</summary>
    void Send(byte[] bytes);
}
