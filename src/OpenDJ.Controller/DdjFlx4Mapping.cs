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
        if (msg.Channel == Channel.Channel11 && msg.Control == 0x40)
        {
            int delta = msg.Value > 64 ? 1 : -1;
            return new ControllerEvent.BrowseRotated(delta);
        }
        return null;
    }
}
