using Sholto.App.Audio;
using Sholto.App.Decks;
using Sholto.App.Library.Markers;

namespace Sholto.App.Tests;

/// <summary>PLAY presses through <see cref="PlaybackRequests"/>: a hot-cue preview in progress is latched and nothing
/// else happens; with no preview, a playing scratch-capable deck brakes and any other deck toggles play.</summary>
public class PlaybackRequestsTests
{
    /// <summary>Hot cues whose preview state the test sets; records each latch asked for.</summary>
    private sealed class LatchingHotCues : IDeckHotCues
    {
        public bool Previewing { get; set; }
        public List<int> Latches { get; } = [];

        public void Attach(IHotCueStore store) { }
        public void Press(int deck, int pad, bool pressed) { }
        public void Delete(int deck, int pad) { }

        public bool LatchPreview(int deck)
        {
            Latches.Add(deck);
            if (!Previewing) return false;
            Previewing = false;
            return true;
        }
    }

    /// <summary>Scripted ports whose scratch control can say it is scratch-capable.</summary>
    private sealed class ScratchablePorts(ScriptedPorts inner, bool canScratch) : IDeckPorts, IDeckScratch
    {
        private readonly ScriptedPorts _inner = inner;
        private readonly bool _canScratch = canScratch;

        public ITransportControl Transport => _inner.Transport;
        public IDeckLooping Looping => _inner.Looping;
        public IDeckBeatgrid Beatgrid => _inner.Beatgrid;
        public IDeckTempo Tempo => _inner.Tempo;
        public IDeckMixer Mixer => _inner.Mixer;
        public IDeckEffects Effects => _inner.Effects;
        public IStemControl Stems => _inner.Stems;
        public ITrackLoading Loading => _inner.Loading;
        public IDeckScratch Scratch => this;
        public IDeckPlayhead Playhead => _inner.Playhead;
        public IDeckReset Reset => _inner.Reset;
        public IEngineDeck Engine => _inner.Engine;

        public bool CanScratch => _canScratch;
        public void ScratchRate(double rate) { }
        public void EndScratch() { }
    }

    private readonly LatchingHotCues _hotCues = new();
    private readonly List<int> _brakes = [];
    private ScriptedPorts _ports = null!;
    private PlaybackRequests _playback = null!;

    private void Build(bool canScratch, bool playing)
    {
        _ports = new ScriptedPorts(new TestDeckFactory().Create());
        _ports.ScriptedLoading.IsPlaying = playing;
        var deck1 = new DeckSessionRig(new ScratchablePorts(_ports, canScratch)).Session;
        var deck2 = new DeckSessionRig(new ScriptedPorts(new TestDeckFactory().Create())).Session;
        _playback = new PlaybackRequests(new DeckPair(deck1, deck2), _hotCues);
        _playback.BrakePauseRequested += _brakes.Add;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PLAY_during_a_preview_latches_it_and_neither_toggles_nor_brakes(bool canScratch)
    {
        Build(canScratch, playing: true);
        _hotCues.Previewing = true;

        _playback.OnPlayPressed(0);

        Assert.Equal([0], _hotCues.Latches);
        Assert.Empty(_brakes);
        Assert.Equal(0, _ports.Spy.TogglePlayCalls);
        Assert.True(_ports.ScriptedLoading.IsPlaying);
    }

    [Fact]
    public void PLAY_with_no_preview_on_a_playing_scratch_capable_deck_brakes()
    {
        Build(canScratch: true, playing: true);

        _playback.OnPlayPressed(0);

        Assert.Equal([0], _hotCues.Latches);
        Assert.Equal([0], _brakes);
        Assert.Equal(0, _ports.Spy.TogglePlayCalls);
    }

    [Fact]
    public void PLAY_with_no_preview_on_a_playing_deck_that_cannot_scratch_pauses()
    {
        Build(canScratch: false, playing: true);

        _playback.OnPlayPressed(0);

        Assert.Empty(_brakes);
        Assert.Equal(1, _ports.Spy.TogglePlayCalls);
        Assert.False(_ports.ScriptedLoading.IsPlaying);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PLAY_with_no_preview_on_a_paused_deck_starts_it(bool canScratch)
    {
        Build(canScratch, playing: false);

        _playback.OnPlayPressed(0);

        Assert.Empty(_brakes);
        Assert.Equal(1, _ports.Spy.TogglePlayCalls);
        Assert.True(_ports.ScriptedLoading.IsPlaying);
    }
}
