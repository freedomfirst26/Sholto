using RtMidi.Core.Enums;
using RtMidi.Core.Messages;

namespace CommunityDj.Controller.Mappings;

/// <summary>
/// Pioneer DDJ-FLX4 mapping.
///
/// Channels (RtMidi 1-indexed; wire value = enum value):
///   1 / 2     Deck 1 / Deck 2 transport + jog wheels
///   7         Top scroll-wheel cluster (browse + Load 1 / Load 2)
///   11        Big mixer-section browse rotary (legacy on some firmwares)
/// </summary>
public sealed class DdjFlx4Mapping : IControllerMapping
{
    public string DeviceNameMatch => "DDJ-FLX4";

    public ControllerEvent? Translate(NoteOnMessage msg) => (msg.Channel, msg.Key) switch
    {
        (Channel.Channel1,  (Key)0x0B) => new ControllerEvent.PlayPressed(Deck: 0),
        (Channel.Channel2,  (Key)0x0B) => new ControllerEvent.PlayPressed(Deck: 1),

        // Top scroll-wheel cluster — browse press + per-deck LOAD buttons.
        (Channel.Channel7,  (Key)0x41) => new ControllerEvent.BrowsePressed(),
        (Channel.Channel7,  (Key)0x46) => new ControllerEvent.LoadToDeck(Deck: 0),
        (Channel.Channel7,  (Key)0x47) => new ControllerEvent.LoadToDeck(Deck: 1),

        // Legacy big browse rotary press (mixer section).
        (Channel.Channel11, (Key)0x15) => new ControllerEvent.BrowsePressed(),

        _ => null,
    };

    public ControllerEvent? Translate(ControlChangeMessage msg)
    {
        // Crossfader. FLX-4 sends 14-bit value as two CCs on channel 7:
        //   0x1F = MSB (high 7 bits), 0x3F = LSB (low 7 bits, ignored — 128 steps is enough)
        // val 0 = full Deck 1, val 127 = full Deck 2.
        if (msg.Channel == Channel.Channel7 && msg.Control == 0x1F)
            return new ControllerEvent.CrossfaderMoved(msg.Value / 127.0);

        // Per-deck channel volume faders. Same 14-bit pattern:
        //   ch 1 = Deck 1, ch 2 = Deck 2.   0x13 = MSB,  0x33 = LSB (ignored).
        if (msg.Control == 0x13)
        {
            int deck = msg.Channel == Channel.Channel1 ? 0
                     : msg.Channel == Channel.Channel2 ? 1
                     : -1;
            if (deck >= 0) return new ControllerEvent.ChannelVolumeMoved(deck, msg.Value / 127.0);
        }

        // Top scroll-wheel rotation: signed 7-bit delta.
        //   val 1..63    →  positive (forward, e.g. val 1 = +1 tick)
        //   val 65..127  →  negative (back, two's-complement of value)
        if (msg.Channel == Channel.Channel7 && msg.Control == 0x40)
        {
            int delta = msg.Value < 64 ? msg.Value : msg.Value - 128;
            if (delta != 0) return new ControllerEvent.BrowseRotated(delta);
        }

        // Legacy big browse rotary (mixer section, channel 11).
        if (msg.Channel == Channel.Channel11 && msg.Control == 0x40)
        {
            int delta = msg.Value > 64 ? 1 : -1;
            return new ControllerEvent.BrowseRotated(delta);
        }

        // Jog wheel rotation. 0x22 = top platter, 0x21 = side ring. Centered at 0x40.
        if (msg.Control == 0x22 || msg.Control == 0x21)
        {
            int deck = msg.Channel == Channel.Channel1 ? 0
                     : msg.Channel == Channel.Channel2 ? 1
                     : -1;
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
