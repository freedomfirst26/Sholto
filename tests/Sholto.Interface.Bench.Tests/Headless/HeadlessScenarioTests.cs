using System.Text.Json;
using Sholto.Interface.Bench.Headless;
using Sholto.Interface.Bench.Scenario;

namespace Sholto.Interface.Bench.Tests.Headless;

public sealed class HeadlessScenarioTests : IDisposable
{
    private readonly string _wav = Path.Combine(Path.GetTempPath(), $"tone48k-{Guid.NewGuid():N}.wav");

    public HeadlessScenarioTests() => new ToneFixture(440, 2).WriteWav(_wav);

    public void Dispose() => File.Delete(_wav);

    private IHeadlessHostFactory NewFactory() => new HeadlessTestHostFactory().Create();

    [Fact]
    public void RunScenario_LoadWaitPlayCue_WaitDoesNotThrowAndGesturesChangeState()
    {
        var scenario = new ScenarioFactory().Create(
            "{ \"actions\": [ { \"action\": \"load\", \"deck\": 1, \"track\": " + JsonSerializer.Serialize(_wav) + ", \"value\": 1.0 }, " +
            "{ \"action\": \"wait\", \"seconds\": 0.5 }, " +
            "{ \"action\": \"gesture\", \"event\": \"PlayPressed\", \"deck\": 1 }, " +
            "{ \"action\": \"gesture\", \"event\": \"CueToggle\", \"deck\": 1 } ] }");

        using var session = NewFactory().Create().RunScenario(scenario);

        var outcomes = session.Driver.GestureOutcomes;
        Assert.Equal(2, outcomes.Count);
        Assert.Equal(true, outcomes[0].Changed["Deck1.IsPlaying"].Before);
        Assert.Equal(false, outcomes[0].Changed["Deck1.IsPlaying"].After);
        Assert.Equal(false, outcomes[1].Changed["Deck1.CueActive"].Before);
        Assert.Equal(true, outcomes[1].Changed["Deck1.CueActive"].After);
        Assert.True(session.Core.Decks.Deck1.Playhead.PositionFrames > 0);
    }
}
