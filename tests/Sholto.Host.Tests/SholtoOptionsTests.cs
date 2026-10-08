namespace Sholto.Host.Tests;

/// <summary>The Host-level option defaults keep the values they had before they became options.</summary>
public class SholtoOptionsTests
{
    [Fact]
    public void Cuda_probe_timeout_defaults_to_sixty_seconds() =>
        Assert.Equal(TimeSpan.FromSeconds(60), new StemSeparationOptions().CudaProbeTimeout);

    [Fact]
    public void The_glance_slot_and_the_platter_share_one_fallback_tempo()
    {
        var options = new SholtoOptions();

        Assert.Equal(120, options.Scratch.Value.FallbackBpm);
        Assert.Equal(options.Scratch.Value.FallbackBpm, options.GlanceView.Value.FallbackBpm);
    }
}
