using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <inheritdoc cref="INoPeaksFactory"/>
public sealed class NoPeaksFactory : INoPeaksFactory
{
    // The rate is nominal: with no columns there is no time to scale.
    private readonly WaveformPeaks _none = new([], [], [], [], [], 512, 48000);

    public WaveformPeaks None() => _none;
}
