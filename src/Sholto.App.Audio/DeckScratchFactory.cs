namespace Sholto.App.Audio;

public sealed class DeckScratchFactory : IDeckScratchFactory
{
    public IDeckScratchControl Create(
        Func<IVarispeedProvider?> varispeed,
        Func<SoundFlow.Components.SoundPlayer?> player,
        Func<bool> isPlaying,
        Func<float> playbackSpeed,
        Func<long> positionFrames) =>
        new DeckScratch(varispeed, player, isPlaying, playbackSpeed, positionFrames);
}
