using SoundFlow.Abstracts;

namespace Sholto.Audio;

/// <summary>
/// Read-only view of a chain effect for UI/MIDI discovery — its string id and
/// parameter metadata, without exposing DSP state. Implemented by
/// <see cref="DeckEffect"/>; exists as its own interface so
/// <see cref="IDeckEffects.Effects"/> can list effects without leaking
/// <see cref="SoundModifier"/> or <see cref="DeckEffect.SetParam"/>-adjacent
/// concerns into the enumeration itself (the setter is reached through
/// <see cref="IDeckEffects.SetParam"/>, not through this view).
/// </summary>
public interface IDeckEffectInfo
{
    /// <summary>Stable string id used to address this effect —
    /// e.g. "eq3", "filter", "echo".</summary>
    string EffectId { get; }

    /// <summary>This effect's parameter metadata.</summary>
    ReadOnlySpan<EffectParam> Params { get; }
}

/// <summary>
/// Base for a deck effect that is addressable by a string id and a small set
/// of numeric parameters, rather than only by C# type. <see cref="DeckMixer.AttachModifiers"/>
/// still picks out the 3 hardware-bound effects (EQ/filter/echo) by type for
/// their named methods (<c>SetEq</c>/<c>SetFilter</c>/<c>SetEcho</c>) — that
/// lookup is unchanged and stays the hardware path. This is the ADDITIONAL
/// seam: any effect that is a <see cref="DeckEffect"/>, including one a
/// hardcoded type-lookup could never reach (e.g. a fourth effect added later
/// to <see cref="DeckEffectFactories"/>), becomes controllable via
/// <see cref="IDeckEffects.SetParam"/> once it's built into the chain.
///
/// Deliberately minimal this pass: identity, parameter metadata, and a
/// setter. No <c>Mix</c>/wet-dry, no <c>RequestReset</c>, no
/// <c>IsEngaged</c> — those need their own audio-behaviour proof and are
/// separate tasks.
///
/// <see cref="EffectId"/> and <see cref="Params"/> are control-thread-only
/// metadata: nothing on the per-buffer <c>Process</c>/<c>ProcessSample</c>
/// path may read them — that path stays exactly as fast and
/// allocation-free as it was before this change.
/// </summary>
public abstract class DeckEffect : SoundModifier, IDeckEffectInfo
{
    /// <inheritdoc/>
    public abstract string EffectId { get; }

    /// <summary>This effect's parameter metadata. Implementations return a
    /// span over a <c>private static readonly EffectParam[]</c> table — never
    /// a per-call allocation.</summary>
    public abstract ReadOnlySpan<EffectParam> Params { get; }

    /// <summary>Apply a value to one parameter, addressed by the <c>Id</c>
    /// from its <see cref="EffectParam"/> entry in <see cref="Params"/>.
    /// Forwards to the effect's existing public setter — this method holds no
    /// state and no DSP maths of its own. Safe to call from any thread (the
    /// same guarantee as the setter it forwards to).</summary>
    public abstract void SetParam(int paramId, double value);
}
