namespace Sholto.Audio;

/// <summary>
/// A deck's post-mix DSP chain — split out of <see cref="IDeckMixer"/> (which
/// keeps only gain/cue routing). The three named controls
/// (<see cref="SetEq"/>/<see cref="SetFilter"/>/<see cref="SetEcho"/>) are
/// bound to fixed FLX4 hardware (EQ knobs, COLOR knob, PAD FX1 button) and
/// stay named methods with their own gain-curve maths — that hardware mapping
/// is fixed, so those three keep their exact bodies.
///
/// <see cref="Effects"/> and <see cref="SetParam"/> are the additive, generic
/// seam alongside them: any chain member that derives from
/// <see cref="DeckEffect"/> — including one <see cref="DeckMixer.AttachModifiers"/>'s
/// hardcoded type-lookups don't reach — is discoverable and controllable
/// through them. Adding a fourth effect to
/// <see cref="DeckEffectFactories"/> now makes it reachable here with no
/// change to this interface or to <see cref="DeckMixer"/>.
/// </summary>
public interface IDeckEffects
{
    /// <summary>The chain's effects that expose string-addressed parameters,
    /// in chain order. Populated once <c>Deck.AttachEngine</c> has run
    /// <see cref="DeckMixer.AttachModifiers"/>; empty before that.</summary>
    IReadOnlyList<IDeckEffectInfo> Effects { get; }

    /// <summary>Set one parameter on one effect, addressed by
    /// <see cref="IDeckEffectInfo.EffectId"/> and the parameter's
    /// <c>Id</c> from <see cref="IDeckEffectInfo.Params"/>. Forwards to that
    /// effect's <see cref="DeckEffect.SetParam"/>. Silently a no-op for an
    /// unknown effect id or before the chain has been attached — same
    /// tolerance as <see cref="SetEq"/>/<see cref="SetFilter"/>/<see cref="SetEcho"/>.
    /// Safe to call from any thread.</summary>
    void SetParam(string effectId, int paramId, double value);

    /// <summary>Set one of the 3 EQ bands (0=Low, 1=Mid, 2=High).
    /// <paramref name="value"/> is 0..1, 0.5 = unity. Safe to call from any
    /// thread.</summary>
    void SetEq(int band, double value);

    /// <summary>Set the COLOR / FILTER knob position. 0 = full LP, 0.5 =
    /// bypass, 1 = full HP. Safe to call from any thread.</summary>
    void SetFilter(double position);

    /// <summary>True while the beat-synced echo is feeding new input into its
    /// delay line. Does NOT mean the echo is silent when false — a tail can
    /// still be ringing out.</summary>
    bool EchoActive { get; }

    /// <summary>Toggle the beat-synced echo.</summary>
    void SetEcho(bool on);

    /// <summary>Disable the echo and flush its delay line, e.g. on track load
    /// so the outgoing track's tail can't ring into the incoming one. Unlike
    /// <see cref="SetEcho"/>(false), which deliberately lets the tail ring
    /// out, this clears it immediately.</summary>
    void ResetEcho();
}
