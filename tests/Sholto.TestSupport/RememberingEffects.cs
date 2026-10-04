using Sholto.App.Audio;

namespace Sholto.TestSupport;

/// <summary>Deck effects that remember the echo switch. A real deck's echo does nothing until an audio engine
/// attaches its modifiers, so a test with no audio swaps this in to see the switch take.</summary>
internal sealed class RememberingEffects(IDeckEffects inner) : IDeckEffects
{
    private readonly IDeckEffects _inner = inner;

    public IReadOnlyList<IDeckEffectInfo> Effects => _inner.Effects;

    public bool EchoActive { get; private set; }

    public void SetParam(string effectId, int paramId, double value) => _inner.SetParam(effectId, paramId, value);

    public void SetEq(int band, double value) => _inner.SetEq(band, value);

    public void SetFilter(double position) => _inner.SetFilter(position);

    public void SetEcho(bool on) => EchoActive = on;

    public void ResetEcho() => EchoActive = false;
}
