namespace Sholto.App.Audio.Recording;

/// <summary>A single-producer, single-consumer lock-free ring of stereo frames.</summary>
public interface IStereoSampleRing : IStereoSampleWriter, IStereoSampleReader;
