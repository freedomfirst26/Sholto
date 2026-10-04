using System.Runtime.InteropServices;

namespace Sholto.Interface.Controller;

/// <summary>Linux implementation of <see cref="IAlsaRawMidiOpener"/>: walks
/// /proc/asound/cards and opens /dev/snd/midiC&lt;N&gt;D0 for the matching card.</summary>
public sealed class AlsaRawMidiOpener : IAlsaRawMidiOpener
{
    public AlsaRawMidi? Open(string deviceName)
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
            return new AlsaRawMidi(fs);
        }
        return null;
    }
}
