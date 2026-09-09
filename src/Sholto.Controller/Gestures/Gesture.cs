namespace Sholto.Controller.Gestures;

/// <summary>What the DJ did, resolved once, in one place.
/// <para><paramref name="Deck"/> is 0 or 1 for a per-deck gesture, and -1 for a
/// global one (crossfader, browse, the BEAT FX column).</para>
/// <para><paramref name="Source"/> is the event this came from. Identity comes from
/// the recognizer; payload — the jog delta, the EQ value, the fader position —
/// comes from the event. Consumers read both.</para></summary>
public sealed record Gesture(string Id, int Deck, ControllerEvent Source);
