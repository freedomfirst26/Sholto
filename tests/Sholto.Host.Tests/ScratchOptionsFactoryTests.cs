using Sholto.App;

namespace Sholto.Host.Tests;

public class ScratchOptionsFactoryTests
{
    private static ScratchOptions Create(string? value) =>
        new ScratchOptionsFactory(name => name == "SHOLTO_SCRATCH_LOG" ? value : null).Create();

    [Fact]
    public void One_turns_the_log_on() => Assert.True(Create("1").Log);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("true")]
    public void Absent_or_any_other_value_leaves_the_log_off(string? value) => Assert.False(Create(value).Log);

    [Fact]
    public void Other_defaults_are_unchanged()
    {
        var options = Create("1");
        var defaults = new ScratchOptions();

        Assert.Equal(defaults.FallbackBpm, options.FallbackBpm);
        Assert.Equal(defaults.BrakeSeconds, options.BrakeSeconds);
        Assert.Equal(defaults.ScratchSecsPerTick, options.ScratchSecsPerTick);
    }
}
