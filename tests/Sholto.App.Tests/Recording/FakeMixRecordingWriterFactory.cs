using Sholto.App.Audio.Recording;

namespace Sholto.App.Tests.Recording;

/// <summary>Builds <see cref="FakeMixRecordingWriter"/>s and keeps them, newest last.</summary>
internal sealed class FakeMixRecordingWriterFactory : IMixRecordingWriterFactory
{
    public List<FakeMixRecordingWriter> Created { get; } = [];

    public FakeMixRecordingWriter Latest => Created[^1];

    public IMixRecordingWriter Create(IStereoSampleReader reader, IWavSink sink)
    {
        var writer = new FakeMixRecordingWriter();
        Created.Add(writer);
        return writer;
    }
}
