using Sholto.App.Audio;
using Sholto.Data;

namespace Sholto.Host.Tests;

/// <summary>The loop options read their two environment overrides through the injected reader, and a fade
/// override is reported once on the <c>StemMix</c> log.</summary>
public sealed class LoopOptionsFactoryTests
{
    private readonly RecordingLogFactory _logs = new();

    private LoopOptions Create(string? mode, string? fadeMs) =>
        new LoopOptionsFactory(name => name switch
        {
            "SHOLTO_LOOP_MODE" => mode,
            "SHOLTO_LOOP_FADE_MS" => fadeMs,
            _ => null,
        }, _logs).Create();

    [Fact]
    public void A_fade_override_is_applied_and_reported_on_the_stem_mix_log()
    {
        var options = Create(null, "12");

        Assert.Equal(12.0, options.FadeMs);
        Assert.Equal([new LoggedLine("StemMix", LogLevel.Info, "loop fade overridden via SHOLTO_LOOP_FADE_MS=12")], _logs.Lines);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("100")]
    public void No_or_an_out_of_range_fade_keeps_the_default_and_logs_nothing(string? fadeMs)
    {
        var options = Create(null, fadeMs);

        Assert.Equal(new LoopOptions().FadeMs, options.FadeMs);
        Assert.Empty(_logs.Lines);
    }

    [Fact]
    public void The_mode_override_is_applied()
    {
        var mode = new LoopOptions().Mode + "-other";

        Assert.Equal(mode, Create(mode, null).Mode);
    }
}
