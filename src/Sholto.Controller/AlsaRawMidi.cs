using System.Runtime.InteropServices;

namespace Sholto.Controller;

/// <summary>
/// Linux-only: reads MIDI bytes directly from /dev/snd/midiC<N>D0, parses the
/// stream, and emits ControllerEvents. This is the sole MIDI input path — raw
/// ALSA reads work under PipeWire/ALSA without needing a running JACK server.
/// </summary>
internal sealed class AlsaRawMidi : IDisposable
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

    private AlsaRawMidi(FileStream stream)
    {
        _stream = stream;
        _reader = Task.Run(ReadLoop);
    }

    public static AlsaRawMidi? Open(string deviceName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return null;

        // Walk /proc/asound/cards and find the card whose name contains `deviceName`.
        var cardLines = File.Exists("/proc/asound/cards")
            ? File.ReadAllLines("/proc/asound/cards")
            : [];
        for (int i = 0; i < cardLines.Length; i++)
        {
            var line = cardLines[i];
            if (!line.Contains(deviceName, StringComparison.OrdinalIgnoreCase)) continue;

            // Format: " <N> [name           ]: driver - description"
            var trimmed = line.TrimStart();
            int spaceIdx = trimmed.IndexOf(' ');
            if (spaceIdx <= 0) continue;
            if (!int.TryParse(trimmed.AsSpan(0, spaceIdx), out var card)) continue;

            var path = $"/dev/snd/midiC{card}D0";
            if (!File.Exists(path)) continue;
            // ReadWrite so we can send MIDI back to the controller (LED updates,
            // device startup init — see IControllerMapping.StartupInit).
            var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            var midi = new AlsaRawMidi(fs);
            return midi;
        }
        return null;
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

    private static int StatusDataLength(byte status) => (status & 0xF0) switch
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
