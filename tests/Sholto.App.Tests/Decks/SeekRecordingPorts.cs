using Sholto.App.Audio;

namespace Sholto.App.Tests;

/// <summary>The scripted ports with the transport swapped for one that records seeks.</summary>
internal sealed class SeekRecordingPorts(ScriptedPorts inner, ITransportControl transport) : IDeckPorts
{
    private readonly ScriptedPorts _inner = inner;

    public ITransportControl Transport { get; } = transport;
    public IDeckLooping Looping => _inner.Looping;
    public IDeckBeatgrid Beatgrid => _inner.Beatgrid;
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
