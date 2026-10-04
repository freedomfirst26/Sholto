using Sholto.App.Analysis.Stems;

namespace Sholto.App.Audio;

/// <summary>Builds the per-load playback data providers, so <see cref="TrackLoading"/>
/// constructs none itself. Concrete types are returned because TrackLoading holds
/// them in concrete-typed fields and calls their members directly.</summary>
public interface IPlaybackProviderFactory
{
    /// <summary>Whole-track stereo provider with varispeed/scratch support.</summary>
    ScratchDataProvider Scratch(float[] stereoSamples, int sampleRate);

    /// <summary>Stem-mix provider over the separated stems.</summary>
    StemMixDataProvider Stems(StemSamples stemSamples, int sampleRate);
}
