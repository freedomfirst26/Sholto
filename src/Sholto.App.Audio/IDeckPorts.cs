namespace Sholto.App.Audio;

/// <summary>The role-scoped ports of one deck, handed to consumers in place of the whole <see cref="Deck"/>.</summary>
public interface IDeckPorts
{
    ITransportControl Transport { get; }
    IDeckLooping Looping { get; }
    IDeckBeatgrid Beatgrid { get; }
    IDeckTempo Tempo { get; }
    IDeckMixer Mixer { get; }
    IDeckEffects Effects { get; }
    IStemControl Stems { get; }
    ITrackLoading Loading { get; }
    IDeckScratch Scratch { get; }
    IDeckPlayhead Playhead { get; }
    IDeckReset Reset { get; }
    IEngineDeck Engine { get; }
}
