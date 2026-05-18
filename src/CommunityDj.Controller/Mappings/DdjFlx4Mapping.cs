namespace CommunityDj.Controller.Mappings;

/// <summary>
/// Pioneer DDJ-FLX4 mapping. All channel numbers are raw MIDI wire values.
///
/// Channels observed:
///   1 / 2    Deck 1 / Deck 2 transport + jog wheels + channel faders
///   7        Top scroll-wheel cluster, crossfader, LOAD 1 / LOAD 2 buttons
///   11       Legacy big browse rotary on some firmwares
/// </summary>
public sealed class DdjFlx4Mapping : IControllerMapping
{
    public string DeviceNameMatch => "DDJ-FLX4";

    public ControllerEvent? Translate(NoteEvent msg) => (msg.Channel, msg.Key) switch
    {
        (1,  0x0B) => new ControllerEvent.PlayPressed(Deck: 0),
        (2,  0x0B) => new ControllerEvent.PlayPressed(Deck: 1),

        // Top scroll-wheel cluster — browse press + per-deck LOAD buttons.
        (7,  0x41) => new ControllerEvent.BrowsePressed(),
        (7,  0x46) => new ControllerEvent.LoadToDeck(Deck: 0),
        (7,  0x47) => new ControllerEvent.LoadToDeck(Deck: 1),

        // Legacy big browse rotary press (mixer section).
        (11, 0x15) => new ControllerEvent.BrowsePressed(),

        _ => null,
    };

    public ControllerEvent? Translate(CcEvent msg)
    {
        // Crossfader: 14-bit value, ch 7, 0x1F = MSB (LSB at 0x3F ignored — 128 steps is enough).
        if (msg.Channel == 7 && msg.Control == 0x1F)
            return new ControllerEvent.CrossfaderMoved(msg.Value / 127.0);

        // Per-deck channel volume faders: 14-bit, 0x13 = MSB on ch 1 (Deck 1) / ch 2 (Deck 2).
        if (msg.Control == 0x13 && (msg.Channel == 1 || msg.Channel == 2))
            return new ControllerEvent.ChannelVolumeMoved(msg.Channel - 1, msg.Value / 127.0);

        // Per-deck EQ pots (14-bit, MSB only here). FLX-4 sends HI on 0x07, MID on 0x0B,
        // LOW on 0x0F — channel 1 = Deck 1, channel 2 = Deck 2.
        if ((msg.Channel == 1 || msg.Channel == 2) &&
            (msg.Control == 0x07 || msg.Control == 0x0B || msg.Control == 0x0F))
        {
            var band = msg.Control switch
            {
                0x07 => EqBand.High,
                0x0B => EqBand.Mid,
                _    => EqBand.Low,
            };
            return new ControllerEvent.EqMoved(msg.Channel - 1, band, msg.Value / 127.0);
        }

        // Top scroll-wheel rotation: signed 7-bit delta.
        //   val 1..63   →  positive (forward, e.g. val 1 = +1 tick)
        //   val 65..127 →  negative (back, two's-complement)
        if (msg.Channel == 7 && msg.Control == 0x40)
        {
            int delta = msg.Value < 64 ? msg.Value : msg.Value - 128;
            if (delta != 0) return new ControllerEvent.BrowseRotated(delta);
        }

        // Legacy big browse rotary (mixer section).
        if (msg.Channel == 11 && msg.Control == 0x40)
            return new ControllerEvent.BrowseRotated(msg.Value > 64 ? 1 : -1);

        // Jog wheel rotation. 0x22 = top platter, 0x21 = side ring. Centered at 0x40.
        if (msg.Control == 0x22 || msg.Control == 0x21)
        {
            int deck = msg.Channel == 1 ? 0 : msg.Channel == 2 ? 1 : -1;
            if (deck >= 0)
            {
                int delta = msg.Value - 64;
                if (delta != 0)
                {
                    var source = msg.Control == 0x22 ? JogSource.TopPlatter : JogSource.SideRing;
                    return new ControllerEvent.JogRotated(deck, delta, source);
                }
            }
        }

        return null;
    }
}
