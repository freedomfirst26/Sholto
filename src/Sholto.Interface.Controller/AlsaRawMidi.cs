namespace Sholto.Interface.Controller;

/// <summary>
/// Linux-only: reads MIDI bytes directly from /dev/snd/midiC<N>D0, parses the
/// stream, and emits ControllerEvents. This is the sole MIDI input path — raw
/// ALSA reads work under PipeWire/ALSA without needing a running JACK server.
/// </summary>
public sealed class AlsaRawMidi : IDisposable
{
    private readonly FileStream _stream;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _reader;

    public event Action<byte, byte, byte>? MessageReceived; // status, data1, data2

    /// <summary>Fired once when the device goes away mid-session (USB unplug,
    /// or resume-from-autosuspend that invalidates the fd). NOT fired on a
    /// clean Dispose. The supervisor in <see cref="MidiManager"/> listens for
    /// this to start reconnecting.</summary>
    public event Action? Disconnected;

    /// <summary>Built by <see cref="AlsaRawMidiOpener"/> once the device node is open.</summary>
    internal AlsaRawMidi(FileStream stream)
    {
        _stream = stream;
        _reader = Task.Run(ReadLoop);
    }

    /// <summary>Write raw MIDI bytes to the controller (LED updates, device
    /// startup init, etc.).</summary>
    public void SendRaw(byte[] bytes)
    {
        try
        {
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MIDI] send failed: {ex.Message}");
        }
    }

    private async Task ReadLoop()
    {
        var buf = new byte[256];
        byte status = 0; // running status
        var data = new byte[2];
        int dataIdx = 0;
        int needed = 0;

        bool lost = false;
        while (!_cts.IsCancellationRequested)
        {
            int n;
            try { n = await _stream.ReadAsync(buf.AsMemory(0, buf.Length), _cts.Token); }
            catch (OperationCanceledException) { break; }        // clean shutdown
            catch (Exception) { lost = true; break; }            // IOException etc: device gone
            if (n == 0) { lost = true; break; }                  // EOF: char device closed under us

            for (int i = 0; i < n; i++)
            {
                byte b = buf[i];
                if ((b & 0x80) != 0)
                {
                    // Status byte. Realtime messages (>= 0xF8) don't reset running status.
                    if (b >= 0xF8) continue;
                    status = b;
                    dataIdx = 0;
                    needed = StatusDataLength(status);
                    if (needed == 0)
                    {
                        MessageReceived?.Invoke(status, 0, 0);
                    }
                }
                else if (status != 0)
                {
                    data[dataIdx++] = b;
                    if (dataIdx >= needed)
                    {
                        MessageReceived?.Invoke(status, data[0], needed > 1 ? data[1] : (byte)0);
                        dataIdx = 0; // running status: keep `status` for next message
                    }
                }
            }
        }

        // Only announce a real disconnect — a Dispose()-driven cancel is not one.
        if (lost && !_cts.IsCancellationRequested) Disconnected?.Invoke();
    }

    private int StatusDataLength(byte status) => (status & 0xF0) switch
    {
        0x80 => 2, // Note Off
        0x90 => 2, // Note On
        0xA0 => 2, // Poly Aftertouch
        0xB0 => 2, // CC
        0xC0 => 1, // Program Change
        0xD0 => 1, // Channel Aftertouch
        0xE0 => 2, // Pitch Bend
        _    => 0
    };

    public void Dispose()
    {
        _cts.Cancel();
        try { _reader.Wait(500); } catch { }
        _stream.Dispose();
        _cts.Dispose();
    }
}
