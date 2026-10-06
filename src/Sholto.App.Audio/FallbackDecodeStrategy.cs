namespace Sholto.App.Audio;

/// <summary>Tries a preferred decoder and, if it throws (e.g. ffmpeg is not
/// installed or rejects the file), decodes with a fallback instead. Handles an
/// extension only when both can.</summary>
public sealed class FallbackDecodeStrategy(IAudioDecodeStrategy preferred, IAudioDecodeStrategy fallback) : IAudioDecodeStrategy
{
    private readonly IAudioDecodeStrategy _preferred = preferred;
    private readonly IAudioDecodeStrategy _fallback = fallback;

    public bool CanDecode(string extension) =>
        _preferred.CanDecode(extension) && _fallback.CanDecode(extension);

    public float[] Decode(string filePath)
    {
        try
        {
            return _preferred.Decode(filePath);
        }
        catch (Exception)
        {
            return _fallback.Decode(filePath);
        }
    }
}
