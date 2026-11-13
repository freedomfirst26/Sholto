namespace Sholto.App.Audio.Recording;

/// <summary>Opens a recording's WAV file, on the calling (app) thread.</summary>
public interface IWavSinkFactory
{
    /// <summary>Creates <paramref name="path"/>; it fails rather than overwrite a file already there.</summary>
    IWavSink Create(string path);
}
