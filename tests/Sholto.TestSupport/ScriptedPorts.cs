using Sholto.App.Analysis.Analyzers;
using Sholto.App.Audio;

namespace Sholto.TestSupport;

/// <summary>A real deck's ports with the transport, loading and playhead replaced by ones the test
/// controls, so a session can be driven (play, position, load) with no audio behind it.</summary>
internal sealed class ScriptedPorts : IDeckPorts
{
    private readonly IDeckPorts _inner;

    /// <param name="inner">The real deck's ports the rest is taken from.</param>
    /// <param name="analysisProvider">Replaces the deck's analysis provider, or null to keep the real one.</param>
    /// <param name="effects">Replaces the deck's effects (a real deck's echo does nothing until an audio engine is attached), or null.</param>
    public ScriptedPorts(IDeckPorts inner, IAnalysisProvider? analysisProvider = null, IDeckEffects? effects = null)
    {
        _inner = inner;
        Effects = effects ?? inner.Effects;
        ScriptedLoading = new ScriptedLoading(inner.Loading, analysisProvider);
        Spy = new SpyTransport(ScriptedLoading);
    }

    public ScriptedLoading ScriptedLoading { get; }

    public SpyTransport Spy { get; }

    public ScriptedPlayhead ScriptedPlayhead { get; } = new();

    public ITransportControl Transport => Spy;
    public ITrackLoading Loading => ScriptedLoading;
    public IDeckPlayhead Playhead => ScriptedPlayhead;

    public IDeckLooping Looping => _inner.Looping;
    public IDeckBeatgrid Beatgrid => _inner.Beatgrid;
    public IDeckTempo Tempo => _inner.Tempo;
    public IDeckMixer Mixer => _inner.Mixer;
    public IDeckEffects Effects { get; }
    public IStemControl Stems => _inner.Stems;
    public IDeckScratch Scratch => _inner.Scratch;
    public IDeckReset Reset => _inner.Reset;
    public IEngineDeck Engine => _inner.Engine;
}
