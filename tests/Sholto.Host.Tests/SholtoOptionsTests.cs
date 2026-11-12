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

    [Fact]
    public void The_analysis_folder_options_default_to_a_sholto_analysis_folder_and_a_one_gibibyte_margin()
    {
        var options = new SholtoOptions().AnalysisFolder.Value;

        Assert.Equal("Sholto Analysis", options.FolderName);
        Assert.Equal(1L << 30, options.FreeSpaceMargin);
    }

    [Fact]
    public void The_folders_panel_estimates_175_mebibytes_of_stems_per_track() =>
        Assert.Equal(175L * 1024 * 1024, new SholtoOptions().FoldersPanel.Value.StemBytesPerTrackEstimate);
}
