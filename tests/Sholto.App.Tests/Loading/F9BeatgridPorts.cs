using Sholto.App.Audio;

namespace Sholto.App.Tests;

/// <summary>The given ports with the beatgrid replaced, so a test sees what the tune editor asks of it.</summary>
internal sealed class F9BeatgridPorts(IDeckPorts inner, IDeckBeatgrid beatgrid) : IDeckPorts
{
    private readonly IDeckPorts _inner = inner;

    public IDeckBeatgrid Beatgrid { get; } = beatgrid;

    public ITransportControl Transport => _inner.Transport;
    public IDeckLooping Looping => _inner.Looping;
    public IDeckTempo Tempo => _inner.Tempo;
    public IDeckMixer Mixer => _inner.Mixer;
    public IDeckEffects Effects => _inner.Effects;
    public IStemControl Stems => _inner.Stems;
    public ITrackLoading Loading => _inner.Loading;
    public IDeckScratch Scratch => _inner.Scratch;
    public IDeckPlayhead Playhead => _inner.Playhead;
    public IDeckReset Reset => _inner.Reset;
    public IEngineDeck Engine => _inner.Engine;
}
