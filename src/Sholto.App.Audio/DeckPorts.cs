namespace Sholto.App.Audio;

/// <summary>
/// Every port is the <see cref="Deck"/> itself, never an inner component: Deck
/// merges AnalysisUpdated and arbitrates tempo against scratch.
/// </summary>
public sealed class DeckPorts(Deck deck) : IDeckPorts
{
    public ITransportControl Transport { get; } = deck;
    public IDeckLooping Looping { get; } = deck;
    public IDeckBeatgrid Beatgrid { get; } = deck;
    public IDeckTempo Tempo { get; } = deck;
    public IDeckMixer Mixer { get; } = deck;
    public IDeckEffects Effects { get; } = deck;
    public IStemControl Stems { get; } = deck;
    public ITrackLoading Loading { get; } = deck;
    public IDeckScratch Scratch { get; } = deck;
    public IDeckPlayhead Playhead { get; } = deck;
    public IDeckReset Reset { get; } = deck;
    public IEngineDeck Engine { get; } = deck;
}
