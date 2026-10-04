namespace Sholto.App.Audio;

/// <summary>Vinyl-style scratch control: platter velocity drives playback rate.</summary>
public interface IDeckScratch
{
    bool CanScratch { get; }
    void ScratchRate(double rate);
    void EndScratch();
}
