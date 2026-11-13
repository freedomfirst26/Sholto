using System.Globalization;
using System.Text;
using System.Text.Json;
using Sholto.Interface.Bench.Scenario;
using Sholto.Interface.Bench.Tests.Rendering;

namespace Sholto.Interface.Bench.Tests.Headless;

/// <summary>A platter backspin rendered offline through the controller path plays out the way it does
/// live: the gesture clock moves by the audio each wait renders, so the scratch engine's release and coast
/// timings line up with the WAV instead of with the milliseconds the render took.</summary>
public sealed class HeadlessScratchRenderTests : IDisposable
{
    private const int SourceRate = 44100;
    private const double ToneHz = 220;
    private const double ToneSeconds = 10;
    private const double PlaySeconds = 1;
    private const int BackspinTurns = 12;
    private const int BackspinDelta = -62;
    private const double FrameSeconds = 0.016;
    private const double CoastSeconds = 3;
    private const double AudibleRms = 0.01;

    private readonly string _tone = Path.Combine(Path.GetTempPath(), $"tone44k-{Guid.NewGuid():N}.wav");
    private readonly string _out = Path.Combine(Path.GetTempPath(), $"backspin-{Guid.NewGuid():N}.wav");

    public HeadlessScratchRenderTests() => new ToneFixture(ToneHz, ToneSeconds, SourceRate).WriteWav(_tone);

    public void Dispose()
    {
        File.Delete(_tone);
        File.Delete(_out);
    }

    [Fact]
    public void RunScenario_BackspinThenRelease_SecondAfterReleaseAudibleAndScratchEnds()
    {
        var actions = new StringBuilder();
        actions.Append("{ \"action\": \"load\", \"deck\": 1, \"track\": ").Append(JsonSerializer.Serialize(_tone)).Append(", \"value\": 1.0 }, ");
        actions.Append(Wait(PlaySeconds)).Append(", ");
        actions.Append("{ \"action\": \"gesture\", \"event\": \"JogTouch\", \"deck\": 1, \"on\": true }, ");
        for (int turn = 0; turn < BackspinTurns; turn++)
        {
            actions.Append("{ \"action\": \"gesture\", \"event\": \"JogRotated\", \"deck\": 1, \"delta\": ")
                .Append(BackspinDelta.ToString(CultureInfo.InvariantCulture)).Append(", \"jogSource\": \"top\" }, ");
            actions.Append(Wait(FrameSeconds)).Append(", ");
        }
        actions.Append("{ \"action\": \"gesture\", \"event\": \"JogTouch\", \"deck\": 1, \"on\": false }");
        int coastWaits = (int)Math.Ceiling(CoastSeconds / FrameSeconds);
        for (int wait = 0; wait < coastWaits; wait++) actions.Append(", ").Append(Wait(FrameSeconds));
        var scenario = new ScenarioFactory().Create("{ \"actions\": [ " + actions + " ] }");

        using var session = new HeadlessTestHostFactory().Create().Create().RunScenario(scenario, _out);

        var wav = new RenderedWav(_out);
        int frameFrames = (int)(FrameSeconds * wav.SampleRate);
        int releaseFrame = (int)(PlaySeconds * wav.SampleRate) + BackspinTurns * frameFrames;
        Assert.True(wav.Left.Length >= releaseFrame + wav.SampleRate, $"render too short ({wav.Left.Length} frames)");
        double afterRelease = Rms(wav.Left, releaseFrame, wav.SampleRate);
        Assert.True(afterRelease > AudibleRms, $"second after the release is silent (rms {afterRelease})");
        Assert.False(session.Core.Decks.DeckFor(0).IsScratching, "deck 1 is still scratching after the coast");
    }

    private string Wait(double seconds) =>
        "{ \"action\": \"wait\", \"seconds\": " + seconds.ToString(CultureInfo.InvariantCulture) + " }";

    private double Rms(float[] x, int start, int count)
    {
        double sum = 0;
        for (int i = start; i < start + count; i++) sum += (double)x[i] * x[i];
        return Math.Sqrt(sum / count);
    }
}
