using Sholto.Bench.Sound;

namespace Sholto.Bench.Tests;

/// <summary>
/// Exercises <see cref="FfmpegMeasurer.ParseStderr"/> against real ffmpeg stderr
/// captured once (<c>Fixtures/ffmpeg_stderr.txt</c>, from rendering the 6.02 dB
/// discrimination scenario) rather than a hand-written approximation — no ffmpeg
/// binary needed to run this test, just the regexes against known text.
/// </summary>
public sealed class FfmpegMeasurerParserTests
{
    private static string FixtureText => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ffmpeg_stderr.txt"));

    [Fact]
    public void ParseStderr_ReadsDurationFromInputLine()
    {
        var result = FfmpegMeasurer.ParseStderr("some.wav", FixtureText);
        Assert.Equal(2.0, result.DurationSeconds!.Value, precision: 2);
    }

    [Fact]
    public void ParseStderr_ReadsIntegratedLoudnessSummary()
    {
        var result = FfmpegMeasurer.ParseStderr("some.wav", FixtureText);
        Assert.Equal(-8.1, result.Loudness.IntegratedLufs!.Value, precision: 3);
        Assert.Equal(0.0, result.Loudness.LoudnessRangeLu!.Value, precision: 3);
        Assert.Equal(4.2, result.Loudness.TruePeakDbfs!.Value, precision: 3);
    }

    [Fact]
    public void ParseStderr_ReadsBothChannelsFromAstatsBlocks_NotTheOverallBlock()
    {
        var result = FfmpegMeasurer.ParseStderr("some.wav", FixtureText);

        Assert.Equal(2, result.Channels.Count);

        var ch1 = result.Channels[0];
        Assert.Equal(1, ch1.Channel);
        Assert.Equal(3.227958, ch1.PeakLevelDb!.Value, precision: 5);
        Assert.Equal(-11.308669, ch1.RmsLevelDb!.Value, precision: 5);

        var ch2 = result.Channels[1];
        Assert.Equal(2, ch2.Channel);
        Assert.Equal(4.058877, ch2.PeakLevelDb!.Value, precision: 5);
        Assert.Equal(-10.536973, ch2.RmsLevelDb!.Value, precision: 5);
    }

    [Fact]
    public void ParseStderr_PairsSilenceStartWithItsMatchingEnd()
    {
        var result = FfmpegMeasurer.ParseStderr("some.wav", FixtureText);

        var silence = Assert.Single(result.Silences);
        Assert.Equal(0.0, silence.StartSeconds, precision: 3);
        Assert.Equal(0.342333, silence.EndSeconds!.Value, precision: 5);
        Assert.Equal(0.342333, silence.DurationSeconds!.Value, precision: 5);
    }

    [Fact]
    public void ParseStderr_WithNoRecognizableOutput_ReturnsNullsNotThrows()
    {
        var result = FfmpegMeasurer.ParseStderr("some.wav", "not ffmpeg output at all");

        Assert.Null(result.DurationSeconds);
        Assert.Null(result.Loudness.IntegratedLufs);
        Assert.Empty(result.Channels);
        Assert.Empty(result.Silences);
    }
}
