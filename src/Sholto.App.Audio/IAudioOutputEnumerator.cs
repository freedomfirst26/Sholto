namespace Sholto.App.Audio;

public interface IAudioOutputEnumerator
{
    IReadOnlyList<AudioDevice> EnumerateOutputs();
}
