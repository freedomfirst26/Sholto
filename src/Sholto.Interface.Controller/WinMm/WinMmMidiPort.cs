using Sholto.Data;

namespace Sholto.Interface.Controller.WinMm;

/// <summary>
/// The Windows <see cref="IRawMidiPort"/>: one open WinMM input and output pair. WinMM delivers channel
/// messages already packed, so incoming ones are only unpacked; outgoing byte buffers are split into packed
/// short messages. WinMM gives no unplug notification, so the port re-reads the device list every
/// <see cref="WinMmMidiOptions.PollInterval"/> and raises <see cref="Disconnected"/> once when its input or
/// output name has gone; <see cref="MidiManager"/>'s supervisor then reopens through the opener.
/// </summary>
public sealed class WinMmMidiPort : IRawMidiPort
{
    private const int Live = 0;
    private const int Lost = 1;
    private const int Disposed = 2;

    private readonly IWinMmIn _in;
    private readonly IWinMmOut _out;
    private readonly string _inName;
    private readonly string _outName;
    private readonly IWinMmDevices _devices;
    private readonly ILog _log;
    private readonly ITimer _poll;
    private int _state = Live;

    public event Action<byte, byte, byte>? MessageReceived;
    public event Action? Disconnected;

    /// <summary>Built by <see cref="WinMmMidiPortOpener"/> once both sides are open: starts the input and the
    /// device-list poll.</summary>
    internal WinMmMidiPort(IWinMmIn input, IWinMmOut output, string inName, string outName, IWinMmDevices devices,
        TimeSpan pollInterval, TimeProvider time, ILog log)
    {
        _in = input;
        _out = output;
        _inName = inName;
        _outName = outName;
        _devices = devices;
        _log = log;
        _in.MessageReceived += OnRawMessage;
        _in.Start();
        _poll = time.CreateTimer(_ => Poll(), null, pollInterval, pollInterval);
    }

    public void Send(byte[] bytes)
    {
        if (Volatile.Read(ref _state) == Disposed) return;
        int i = 0;
        byte running = 0;
        while (i < bytes.Length)
        {
            byte b = bytes[i];
            byte status;
            if (b >= 0xF8) { status = b; i++; }                  // realtime: one byte, keeps running status
            else if (b == 0xF0 || b == 0xF7)
            {
                _log.Write(LogLevel.Warn, "send dropped a system-exclusive message (WinMM short messages only)");
                return;
            }
            else if (b >= 0x80) { status = b; i++; running = b < 0xF0 ? b : (byte)0; }
            else if (running != 0) status = running;              // running status: data byte starts the message
            else { i++; continue; }                               // stray data byte with no status: skip it

            int length = DataLength(status);
            if (i + length > bytes.Length)
            {
                _log.Write(LogLevel.Warn, $"send dropped a truncated message (status 0x{status:X2})");
                return;
            }
            int message = status;
            if (length > 0) message |= bytes[i] << 8;
            if (length > 1) message |= bytes[i + 1] << 16;
            i += length;
            try { _out.Send(message); }
            catch (Exception ex)
            {
                _log.Write(LogLevel.Warn, $"send failed: {ex.Message}");
                return;
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _state, Disposed) == Disposed) return;
        _poll.Dispose();
        _in.MessageReceived -= OnRawMessage;
        try { _in.Dispose(); }
        catch (Exception ex) { _log.Write(LogLevel.Warn, $"closing {_inName} input failed: {ex.Message}"); }
        try { _out.Dispose(); }
        catch (Exception ex) { _log.Write(LogLevel.Warn, $"closing {_outName} output failed: {ex.Message}"); }
    }

    private void OnRawMessage(int raw)
    {
        if (Volatile.Read(ref _state) == Disposed) return;
        byte status = (byte)(raw & 0xFF);
        if (status < 0x80 || status >= 0xF8) return;              // data-only or realtime: not a message we use
        int length = DataLength(status);
        byte data1 = length > 0 ? (byte)((raw >> 8) & 0x7F) : (byte)0;
        byte data2 = length > 1 ? (byte)((raw >> 16) & 0x7F) : (byte)0;
        MessageReceived?.Invoke(status, data1, data2);
    }

    private void Poll()
    {
        if (Volatile.Read(ref _state) != Live) return;
        bool present;
        try
        {
            present = _devices.InputNames().Contains(_inName, StringComparer.Ordinal)
                      && _devices.OutputNames().Contains(_outName, StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            // A failed read says nothing about the device: keep it, and look again next tick.
            _log.Write(LogLevel.Warn, $"device poll failed: {ex.Message}");
            present = true;
        }
        if (present) return;
        if (Interlocked.CompareExchange(ref _state, Lost, Live) != Live) return;
        _poll.Dispose();
        _log.Write(LogLevel.Info, $"{_inName} is gone from the device list");
        Disconnected?.Invoke();
    }

    private int DataLength(byte status) => status switch
    {
        >= 0x80 and < 0xC0 => 2,  // Note Off, Note On, Poly Aftertouch, CC
        >= 0xC0 and < 0xE0 => 1,  // Program Change, Channel Aftertouch
        >= 0xE0 and < 0xF0 => 2,  // Pitch Bend
        0xF1 or 0xF3 => 1,        // MTC quarter frame, Song Select
        0xF2 => 2,                // Song Position
        _ => 0,
    };
}
