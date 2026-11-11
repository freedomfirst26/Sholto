namespace Sholto.App.Audio;

public interface IDeckScratchFactory
{
    /// <summary>One deck's scratch component over that deck's live accessors.</summary>
    IDeckScratchControl Create(
        Func<IVarispeedProvider?> varispeed,
        Func<SoundFlow.Components.SoundPlayer?> player,
        Func<bool> isPlaying,
        Func<float> playbackSpeed,
        Func<long> positionFrames);
}
