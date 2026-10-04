using Microsoft.Extensions.Options;
using Sholto.App.Dsp;

namespace Sholto.App.Analysis.Analyzers.Waveform;

/// <summary>Default <see cref="IWaveformBandSplitterFactory"/>; holds the band-split recipe.</summary>
public sealed class WaveformBandSplitterFactory(IBiquadFactory biquads, IOptions<WaveformBandOptions> options) : IWaveformBandSplitterFactory
{
    private readonly IBiquadFactory _biquads = biquads;
    private readonly WaveformBandOptions _options = options.Value;

    public IWaveformBandSplitter Create(int sampleRate)
    {
        // Three-band split for the visualiser: a narrow BPF in the middle leaves a
        // hole around 2–4 kHz (snare crack, vocal presence, hi-hat shimmer drop
        // out of every band). Instead, run LP + HP and define mid as the
        // complement — so the three bands sum to the original signal with no gap.
        var lpf = _biquads.LowPass(sampleRate, freq: WaveformBandFrequencies.LowMidHz, q: _options.LowPassQ);
        var hpf = _biquads.HighPass(sampleRate, freq: WaveformBandFrequencies.MidHighHz, q: _options.HighPassQ);
        return new WaveformBandSplitter(lpf, hpf);
    }
}
