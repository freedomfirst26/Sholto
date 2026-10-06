using Sholto.App.Lifecycle;
using Sholto.App.Settings;
using Sholto.Data;

namespace Sholto.App.Tests;

public class HintCounterTests
{
    private static readonly Origin Origin = new(InterfaceIds.Bench, "test", "shown");

    private static Task<int> Count(HintCounter counter, string key) => counter.Handle(new GetHintShownCount(key));

    /// <summary>The fake database only "opens" once told to, as the app does after first paint.</summary>
    private static AppLifecycleRig OpenedRig(bool databaseAvailable = true)
    {
        var rig = new AppLifecycleRig(databaseAvailable);
        rig.Database.OpenAsync(_ => Task.CompletedTask).GetAwaiter().GetResult();
        return rig;
    }

    [Fact]
    public async Task Each_recorded_hint_adds_one_and_is_saved_under_the_key()
    {
        var rig = OpenedRig();
        var counter = new HintCounter(rig.Database);

        counter.Handle(new RecordHintShown("faceplate", Origin));
        counter.Handle(new RecordHintShown("faceplate", Origin));

        Assert.Equal(2, await Count(counter, "faceplate"));
        Assert.Equal("2", rig.Settings.Peek(SettingsKeys.HintShownPrefix + "faceplate"));
    }

    [Fact]
    public async Task Two_keys_count_independently()
    {
        var rig = OpenedRig();
        var counter = new HintCounter(rig.Database);

        counter.Handle(new RecordHintShown("faceplate", Origin));
        counter.Handle(new RecordHintShown("system_report", Origin));
        counter.Handle(new RecordHintShown("system_report", Origin));

        Assert.Equal(1, await Count(counter, "faceplate"));
        Assert.Equal(2, await Count(counter, "system_report"));
        Assert.Equal(0, await Count(counter, "other"));
    }

    [Fact]
    public async Task The_count_survives_a_restart()
    {
        var rig = OpenedRig();
        var first = new HintCounter(rig.Database);
        first.Handle(new RecordHintShown("faceplate", Origin));
        first.Handle(new RecordHintShown("faceplate", Origin));
        await Count(first, "faceplate");

        var afterRestart = new HintCounter(rig.Database);

        Assert.Equal(2, await Count(afterRestart, "faceplate"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Faceplate")]
    [InlineData("face-plate")]
    [InlineData("face plate")]
    [InlineData("faceplate1")]
    [InlineData("a:b")]
    public async Task A_key_outside_lowercase_letters_and_underscores_is_rejected(string key)
    {
        var rig = OpenedRig();
        var counter = new HintCounter(rig.Database);

        counter.Handle(new RecordHintShown(key, Origin));

        Assert.Equal(0, await Count(counter, key));
        Assert.Null(rig.Settings.Peek(SettingsKeys.HintShownPrefix + key));
    }

    [Fact]
    public async Task Without_a_database_nothing_is_saved_and_the_count_is_zero()
    {
        var rig = OpenedRig(databaseAvailable: false);
        var counter = new HintCounter(rig.Database);

        counter.Handle(new RecordHintShown("faceplate", Origin));

        Assert.Equal(0, await Count(counter, "faceplate"));
    }
}
