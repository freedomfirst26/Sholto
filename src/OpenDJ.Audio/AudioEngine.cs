using PortAudioSharp;
using PAStream = PortAudioSharp.Stream;

namespace OpenDJ.Audio;

public sealed class AudioEngine : IAudioOutput
{
    private const int SampleRate = 44100;
    private const int FramesPerBuffer = 256;
    private const int Channels = 2;

    private readonly DeckPlayer _deckA;
    private PAStream? _stream;
    private bool _running;

    // Pre-allocated — never allocate on audio thread
    private readonly float[] _mixBuffer = new float[FramesPerBuffer * Channels];

    public bool IsRunning => _running;

    public AudioEngine(DeckPlayer deckA)
    {
        _deckA = deckA;
    }

    public void Start()
    {
        PortAudio.Initialize();

        int device = PortAudio.DefaultOutputDevice;
        if (device == PortAudio.NoDevice)
            throw new InvalidOperationException("No audio output device found.");

        var outParams = new StreamParameters
        {
            device = device,
            channelCount = Channels,
            sampleFormat = SampleFormat.Float32,
            suggestedLatency = PortAudio.GetDeviceInfo(device).defaultLowOutputLatency
        };

        PAStream.Callback cb = AudioCallback;
        _stream = new PAStream(
            inParams: null,
            outParams: outParams,
            sampleRate: SampleRate,
            framesPerBuffer: FramesPerBuffer,
            streamFlags: StreamFlags.ClipOff,
            callback: cb,
            userData: IntPtr.Zero
        );

        _stream.Start();
        _running = true;
    }

    public void Stop()
    {
        _stream?.Stop();
        _stream?.Dispose();
        _stream = null;
        _running = false;
        PortAudio.Terminate();
    }

    public void Dispose() => Stop();

    private StreamCallbackResult AudioCallback(
        IntPtr input, IntPtr output,
        uint frameCount,
        ref StreamCallbackTimeInfo timeInfo,
        StreamCallbackFlags statusFlags,
        IntPtr userData)
    {
        int frames = (int)frameCount;
        int sampleCount = frames * Channels;

        _deckA.FillBuffer(_mixBuffer, frames);

        unsafe
        {
            float* outBuf = (float*)(void*)output;
            for (int i = 0; i < sampleCount; i++)
                outBuf[i] = _mixBuffer[i];
        }

        return StreamCallbackResult.Continue;
    }
}
