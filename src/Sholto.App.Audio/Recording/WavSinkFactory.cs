namespace Sholto.App.Audio.Recording;

/// <summary>See <see cref="IWavSinkFactory"/>: a 48 kHz stereo IEEE-float <see cref="WavSink"/>.</summary>
public sealed class WavSinkFactory : IWavSinkFactory
{
    public IWavSink Create(string path) => new WavSink(new FileStream(path, FileMode.CreateNew, FileAccess.Write));
}
