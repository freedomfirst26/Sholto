namespace Sholto.App.Audio.Recording;

/// <summary>Builds the writer for one recording.</summary>
public interface IMixRecordingWriterFactory
{
    IMixRecordingWriter Create(IStereoSampleReader reader, IWavSink sink);
}
