namespace Sholto.Host.Tests;

public class AudioTimingOptionsFactoryTests
{
    private static bool Enabled(string? value) =>
        new AudioTimingOptionsFactory(name => name == "SHOLTO_AUDIO_TIMING" ? value : null).Create().Enabled;

    [Fact]
    public void One_turns_timing_on() => Assert.True(Enabled("1"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("true")]
    public void Absent_or_any_other_value_leaves_timing_off(string? value) => Assert.False(Enabled(value));
}
