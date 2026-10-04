using SoundFlow.Abstracts;

namespace Sholto.App.Audio;

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
