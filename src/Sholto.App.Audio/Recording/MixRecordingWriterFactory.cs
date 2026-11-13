using Microsoft.Extensions.Options;
using Sholto.App.Analysis;

namespace Sholto.App.Audio.Recording;

/// <summary>See <see cref="IMixRecordingWriterFactory"/>. Reads the drain and flush intervals once.</summary>
public sealed class MixRecordingWriterFactory(IOptions<MixRecordingOptions> options) : IMixRecordingWriterFactory
{
    private readonly TimeSpan _drainInterval = TimeSpan.FromMilliseconds(options.Value.DrainIntervalMs);
    private readonly long _flushIntervalFrames = (long)options.Value.FlushIntervalSeconds * EngineAudio.SampleRate;

    public IMixRecordingWriter Create(IStereoSampleReader reader, IWavSink sink) =>
        new MixRecordingWriter(reader, sink, _drainInterval, _flushIntervalFrames);
}
