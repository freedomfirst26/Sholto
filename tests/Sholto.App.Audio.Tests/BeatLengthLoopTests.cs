using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Stems;
using Xunit;

namespace Sholto.App.Audio.Tests;

/// <summary>A loop of a length in beats on the real loop logic: a 120 BPM track (a beat every 0.5 s, so a
/// beat is 24000 frames = 48000 interleaved samples at 48 kHz), the playhead on the beat at 1 s.</summary>
public class BeatLengthLoopTests
{
    private const int Rate = 48000;
    private const double Bpm = 120;
    private const double TrackSeconds = 40;
    private const long SamplesPerBeat = Rate / 2 * 2;   // half a second, interleaved stereo

    private readonly StemMixDataProvider _stems = new(new StemSamples([], [], [], []), Rate, Options.Create(new LoopOptions()));
    private readonly TrackAnalysis _analysis = new();
    private long _positionFrames = Rate;   // 1 s: beat 2

    private IDeckLooping Make()
    {
        var beats = Enumerable.Range(0, (int)(TrackSeconds * 2)).Select(i => i * 0.5).ToArray();
        var downbeats = beats.Where((_, i) => i % 4 == 0).ToArray();
        _analysis.Set(new BasicAnalysis(null!, Bpm, beats, downbeats));
        return new DeckLooping(() => _stems, () => _analysis, () => _positionFrames, () => (long)(TrackSeconds * Rate),
            Options.Create(new LoopOptions()), new RecordingLogFactory().Create("Deck"));
    }

    [Fact]
    public void A_quarter_beat_loop_is_a_quarter_of_a_beat_long_and_starts_on_the_beat()
    {
        var looping = Make();

        looping.EnableBeatLoopBeats(0.25);

        var loop = Assert.NotNull(looping.ActiveLoop);
        Assert.Equal(2 * SamplesPerBeat, loop.StartSample);
        Assert.Equal(SamplesPerBeat / 4, loop.LengthSamples);
        Assert.Equal(loop, _stems.ActiveLoop);
    }

    [Fact]
    public void A_thirty_two_beat_loop_is_thirty_two_beats_long()
    {
        var looping = Make();

        looping.EnableBeatLoopBeats(32);

        var loop = Assert.NotNull(looping.ActiveLoop);
        Assert.Equal(2 * SamplesPerBeat, loop.StartSample);
        Assert.Equal(32 * SamplesPerBeat, loop.LengthSamples);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void Every_other_beat_loop_pad_length_loops_that_many_beats(double beats)
    {
        var looping = Make();

        looping.EnableBeatLoopBeats(beats);

        Assert.Equal((long)(beats * SamplesPerBeat), Assert.NotNull(looping.ActiveLoop).LengthSamples);
    }

    [Fact]
    public void A_second_press_exits_the_loop_whatever_its_length()
    {
        var looping = Make();
        looping.EnableBeatLoopBeats(0.25);

        looping.EnableBeatLoopBeats(0.25);

        Assert.Null(looping.ActiveLoop);
        Assert.Null(_stems.ActiveLoop);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(0.3)]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_length_that_is_neither_whole_bars_nor_a_halving_of_a_bar_is_ignored(double beats)
    {
        var looping = Make();

        looping.EnableBeatLoopBeats(beats);

        Assert.Null(looping.ActiveLoop);
    }
}
