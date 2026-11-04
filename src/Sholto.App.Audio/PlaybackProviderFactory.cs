using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Stems;

namespace Sholto.App.Audio;

/// <summary>Default <see cref="IPlaybackProviderFactory"/>. Called on track load,
/// never from an audio callback.</summary>
public sealed class PlaybackProviderFactory(IOptions<LoopOptions> loopOptions) : IPlaybackProviderFactory
{
    private readonly IOptions<LoopOptions> _loopOptions = loopOptions;

    public ScratchDataProvider Scratch(float[] stereoSamples, int sampleRate) => new(stereoSamples, sampleRate);

    public StemMixDataProvider Stems(StemSamples stemSamples, int sampleRate) => new(stemSamples, sampleRate, _loopOptions);
}
