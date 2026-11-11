using Sholto.App.Audio;

namespace Sholto.App.Tests;

/// <summary>The scripted ports with the loading swapped for one that replaces the track analysis on every
/// load, as the real player does.</summary>
internal sealed class AnalysisSwappingPorts(ScriptedPorts inner) : IDeckPorts
{
    private readonly ScriptedPorts _inner = inner;

    public ITrackLoading Loading { get; } = new AnalysisSwappingLoading(inner.Loading);

    public ITransportControl Transport => _inner.Transport;
    public IDeckLooping Looping => _inner.Looping;
    public IDeckBeatgrid Beatgrid => _inner.Beatgrid;
    public IDeckTempo Tempo => _inner.Tempo;
    public IDeckMixer Mixer => _inner.Mixer;
    public IDeckEffects Effects => _inner.Effects;
    public IStemControl Stems => _inner.Stems;
    public IDeckScratch Scratch => _inner.Scratch;
    public IDeckPlayhead Playhead => _inner.Playhead;
    public IDeckReset Reset => _inner.Reset;
    public IEngineDeck Engine => _inner.Engine;
}
