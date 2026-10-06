using Sholto.App.Analysis.Harmony;

namespace Sholto.App.Analysis.Tests;

public class KeyCompatibilityTests
{
    [Fact]
    public void The_same_key_is_Perfect()
    {
        Assert.Equal(HarmonicMatchResult.Perfect, new Key(0, true).Compatibility(new Key(0, true)));
    }

    [Fact]
    public void The_relative_minor_is_Perfect()
    {
        Assert.Equal(HarmonicMatchResult.Perfect, new Key(0, true).Compatibility(new Key(9, false)));
    }

    [Fact]
    public void One_step_either_way_on_the_same_ring_is_Close()
    {
        Assert.Equal(HarmonicMatchResult.Close, new Key(0, true).Compatibility(new Key(7, true)));
        Assert.Equal(HarmonicMatchResult.Close, new Key(0, true).Compatibility(new Key(5, true)));
    }

    [Fact]
    public void One_step_on_the_other_ring_is_EnergyBoost()
    {
        Assert.Equal(HarmonicMatchResult.EnergyBoost, new Key(0, true).Compatibility(new Key(4, false)));
    }

    [Fact]
    public void Distant_keys_are_Far()
    {
        Assert.Equal(HarmonicMatchResult.Far, new Key(0, true).Compatibility(new Key(6, true)));
    }

    [Fact]
    public void Compatibility_wraps_around_the_ring()
    {
        // 1B (pitch 11) and 12B (pitch 4) are one step apart across the 12 -> 1 seam.
        Assert.Equal(HarmonicMatchResult.Close, new Key(11, true).Compatibility(new Key(4, true)));
    }

    [Fact]
    public void Compatibility_is_symmetric()
    {
        var a = new Key(0, true);
        var b = new Key(4, false);
        Assert.Equal(a.Compatibility(b), b.Compatibility(a));
    }

    [Fact]
    public void MixableKeys_for_8B_are_8B_8A_7B_9B_7A_9A()
    {
        var eightB = new Key(0, true);

        var codes = eightB.MixableKeys().Select(k => k.ToCamelot()).OrderBy(c => c).ToArray();

        Assert.Equal(new[] { "7A", "7B", "8A", "8B", "9A", "9B" }, codes);
    }
}
