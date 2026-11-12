using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Stems;
using Sholto.Data;
using Xunit;

namespace Sholto.App.Audio.Tests;

public class DeckLoopingLogTests
{
    private const int SampleRate = 48000;
    private const int Frames = 10 * SampleRate;

    [Fact]
    public void Exiting_a_loop_logs_loop_OFF_at_Info_under_the_Deck_tag()
    {
        var logs = new RecordingLogFactory();
        var silence = new float[Frames * 2];
        var stems = new StemMixDataProvider(new StemSamples(silence, silence, silence, silence), SampleRate,
            Options.Create(new LoopOptions()));
        var analysis = new TrackAnalysis();
        double[] beats = Enumerable.Range(0, 20).Select(i => i * 0.5).ToArray();
        double[] downbeats = beats.Where((_, i) => i % 4 == 0).ToArray();
        analysis.Set(new BasicAnalysis(null!, 120, beats, downbeats));
        var looping = new DeckLooping(
            stemProvider: () => stems,
            analysis: () => analysis,
            positionFrames: () => 0,
            sampleCount: () => Frames,
            options: Options.Create(new LoopOptions()),
            log: logs.Create("Deck"));
        looping.EnableBeatLoop(1);
        Assert.NotNull(looping.ActiveLoop);

        looping.ExitLoop();

        Assert.Equal(new LoggedLine("Deck", LogLevel.Info, "loop OFF"), logs.Lines[^1]);
    }
}
