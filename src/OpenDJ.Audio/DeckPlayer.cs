namespace OpenDJ.Audio;

public sealed class DeckPlayer
{
    private float[] _samples = [];
    private int _sampleRate = 44100;
    private long _positionFrames;
    private bool _isPlaying;

    public bool IsLoaded => _samples.Length > 0;
    public bool IsPlaying => _isPlaying;
    public long PositionFrames => _positionFrames;
    public WaveformPeaks Peaks { get; private set; } = WaveformPeaks.Empty;

    public double PlayPosition
    {
        get
        {
            long total = _samples.Length / 2;
            return total == 0 ? 0.0 : Math.Clamp((double)_positionFrames / total, 0.0, 1.0);
        }
    }

    public void Load(float[] stereoSamples, int sampleRate)
    {
        _isPlaying = false;
        _positionFrames = 0;
        _samples = stereoSamples;
        _sampleRate = sampleRate;
        Peaks = WaveformPeaks.Compute(stereoSamples, channels: 2);
    }

    public void Play() => _isPlaying = true;
    public void Pause() => _isPlaying = false;
    public void TogglePlay() { if (_isPlaying) Pause(); else Play(); }

    // Called on the audio thread — no allocations.
    public void FillBuffer(float[] buffer, int frameCount)
    {
        int sampleCount = frameCount * 2;

        if (!_isPlaying || _samples.Length == 0)
        {
            Array.Clear(buffer, 0, sampleCount);
            return;
        }

        long available = (_samples.Length / 2) - _positionFrames;
        long framesToCopy = Math.Min(frameCount, available);
        long srcOffset = _positionFrames * 2;

        Array.Copy(_samples, srcOffset, buffer, 0, framesToCopy * 2);

        if (framesToCopy < frameCount)
        {
            Array.Clear(buffer, (int)(framesToCopy * 2), (int)((frameCount - framesToCopy) * 2));
            _isPlaying = false;
        }

        _positionFrames += framesToCopy;
    }
}
