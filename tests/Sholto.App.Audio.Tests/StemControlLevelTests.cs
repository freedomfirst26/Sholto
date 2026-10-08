using Sholto.App.Analysis.Stems;
using Xunit;

namespace Sholto.App.Audio.Tests;

/// <summary>The stem level handed to <see cref="IStemControl.SetStemGroupLevel"/> is the stem's gain
/// (1.0 = unity): unity must play the stem at full, and 0 must silence it.</summary>
public class StemControlLevelTests
{
    private const int Frames = 256;

    private static float DrumsOutput(double level)
    {
        var drums = new float[Frames * 2];
        Array.Fill(drums, 1f);
        var silent = new float[Frames * 2];
        var provider = new StemMixDataProvider(new StemSamples(drums, silent, silent, silent), 48000);
        new StemControl(() => provider).SetStemGroupLevel(0, level);
        var buffer = new float[64];
        provider.ReadBytes(buffer);
        return buffer[10];
    }

    [Fact]
    public void Unity_level_plays_the_stem_at_the_same_output_as_an_untouched_stem()
    {
        var untouched = DrumsOutput(1.0);
        Assert.True(untouched > 0.1f, $"untouched output {untouched}");
        Assert.Equal(untouched, DrumsOutput(1.5), 5);
    }

    [Fact]
    public void Zero_level_silences_the_stem_and_half_level_is_in_between()
    {
        var unity = DrumsOutput(1.0);
        Assert.Equal(0f, DrumsOutput(0.0), 5);
        var half = DrumsOutput(0.5);
        Assert.InRange(half, unity * 0.3f, unity * 0.7f);
    }
}
