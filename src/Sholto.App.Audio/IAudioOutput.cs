namespace Sholto.App.Audio;

public interface IAudioOutput : IDisposable
{
    void Start();
    void Stop();
    bool IsRunning { get; }
}
