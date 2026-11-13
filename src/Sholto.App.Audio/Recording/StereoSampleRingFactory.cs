using Microsoft.Extensions.Options;
using Sholto.App.Analysis;

namespace Sholto.App.Audio.Recording;

/// <summary>Sizes each ring to <see cref="MixRecordingOptions.RingSeconds"/> of audio at the engine rate.</summary>
public sealed class StereoSampleRingFactory(IOptions<MixRecordingOptions> options) : IStereoSampleRingFactory
{
    private readonly int _capacityFrames = options.Value.RingSeconds * EngineAudio.SampleRate;

    public IStereoSampleRing Create() => new StereoSampleRing(_capacityFrames);
}
