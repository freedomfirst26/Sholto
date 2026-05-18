using System.Runtime.InteropServices;

namespace OpenDJ.Controller;

/// <summary>
/// Linux-only: reads MIDI bytes directly from /dev/snd/midiC<N>D0, parses the
/// stream, and emits ControllerEvents. Used when RtMidi.Core fails to enumerate
/// the device (e.g. PipeWire system without a running JACK server).
/// </summary>
internal sealed class AlsaRawMidi : IDisposable
{
    private readonly FileStream _stream;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _reader;

    public event Action<byte, byte, byte>? MessageReceived; // status, data1, data2

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
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return new AlsaRawMidi(fs);
        }
        return null;
    }

    private async Task ReadLoop()
    {
        var buf = new byte[256];
        byte status = 0; // running status
        var data = new byte[2];
        int dataIdx = 0;
        int needed = 0;

        while (!_cts.IsCancellationRequested)
        {
            int n;
            try { n = await _stream.ReadAsync(buf.AsMemory(0, buf.Length), _cts.Token); }
            catch (OperationCanceledException) { break; }
            catch (IOException) { break; }

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
