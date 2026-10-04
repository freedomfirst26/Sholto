using Sholto.App.Analysis.Stems;

namespace Sholto.App.Audio;

/// <summary>Default <see cref="IPlaybackProviderFactory"/>. Called on track load,
/// never from an audio callback.</summary>
public sealed class PlaybackProviderFactory : IPlaybackProviderFactory
{
    public ScratchDataProvider Scratch(float[] stereoSamples, int sampleRate) => new(stereoSamples, sampleRate);

    public StemMixDataProvider Stems(StemSamples stemSamples, int sampleRate) => new(stemSamples, sampleRate);
}
