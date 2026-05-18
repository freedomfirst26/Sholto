using RtMidi.Core.Enums;
using RtMidi.Core.Messages;

namespace OpenDJ.Controller;

public static class DdjFlx4Mapping
{
    public static ControllerEvent? Translate(NoteOnMessage msg) =>
        (msg.Channel, msg.Key) switch
        {
            (Channel.Channel1, (RtMidi.Core.Enums.Key)0x0B) => new ControllerEvent.PlayPressed(Deck: 0),
            (Channel.Channel2, (RtMidi.Core.Enums.Key)0x0B) => new ControllerEvent.PlayPressed(Deck: 1),
            (Channel.Channel11, (RtMidi.Core.Enums.Key)0x15) => new ControllerEvent.BrowsePressed(),
            _ => null
        };

    public static ControllerEvent? Translate(ControlChangeMessage msg)
    {
        // Browse rotary (mixer channel 11, CC 0x40).
        if (msg.Channel == Channel.Channel11 && msg.Control == 0x40)
        {
            int delta = msg.Value > 64 ? 1 : -1;
            return new ControllerEvent.BrowseRotated(delta);
        }

        // Jog wheel rotation.
        // FLX-4: CC 0x22 = top platter, CC 0x21 = side ring. Both centered at 0x40.
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
