namespace Sholto.Interface.Controller.Gestures;

/// <summary>What the DJ did, resolved once, in one place, and private to the controller interface: the
/// App never sees it, only the command the interface turns it into.
/// <para><paramref name="Deck"/> is 0 or 1 for a per-deck gesture, and -1 for a global one (crossfader,
/// browse, the BEAT FX column).</para>
/// <para><paramref name="Source"/> is the raw event this came from; identity comes from the recognizer,
/// payload (the jog delta, the EQ value, the fader position) from the event. A struct, so recognising
/// an event allocates nothing.</para></summary>
internal readonly record struct Gesture(string Id, int Deck, ControllerEvent Source);
