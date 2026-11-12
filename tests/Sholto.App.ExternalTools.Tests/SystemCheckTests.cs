using Sholto.Data;

namespace Sholto.App.ExternalTools.Tests;

/// <summary>The boot-time tool probe as the event the interfaces are told.</summary>
public class SystemCheckTests
{
    private ToolPresence BeatThis(bool present) =>
        new(ExternalToolNames.BeatThis, ToolCapabilities.Beats, true, present ? "/usr/bin/beat_this" : null);

    private ToolPresence Madmom(bool present) =>
        new(ExternalToolNames.Madmom, ToolCapabilities.Beats, false, present ? "/usr/bin/DBNDownBeatTracker" : null);

    private ToolPresence Demucs(bool present) =>
        new(ExternalToolNames.Demucs, ToolCapabilities.Stems, false, present ? "/usr/bin/demucs" : null);

    [Fact]
    public void ToReported_carries_every_field_both_beat_install_commands_and_the_degraded_health()
    {
        var check = new SystemCheck([BeatThis(true), Madmom(false), Demucs(false)]);

        var reported = check.ToReported();

        Assert.Equal(SystemHealth.Degraded, reported.Health);
        Assert.Equal(
            [
                new ToolStatus(ExternalToolNames.BeatThis, ToolCapabilities.Beats, true, "/usr/bin/beat_this", BeatThisBeatAnalysisStep.InstallCommand),
                new ToolStatus(ExternalToolNames.Madmom, ToolCapabilities.Beats, false, null, MadmomBeatAnalysisStep.InstallCommand),
                new ToolStatus(ExternalToolNames.Demucs, ToolCapabilities.Stems, false, null, null),
            ],
            reported.Tools);
    }

    [Fact]
    public void Beat_this_alone_covers_beats_so_a_missing_madmom_costs_nothing()
    {
        var check = new SystemCheck([BeatThis(true), Madmom(false), Demucs(true)]);

        Assert.False(check.AnyRequiredMissing);
        Assert.Equal(SystemStatus.Healthy, check.Status);
    }

    [Fact]
    public void Madmom_alone_still_covers_beats_so_the_app_is_not_offline()
    {
        var check = new SystemCheck([BeatThis(false), Madmom(true), Demucs(true)]);

        Assert.False(check.AnyRequiredMissing);
        Assert.Equal(SystemStatus.Healthy, check.Status);
    }

    [Fact]
    public void Neither_beat_tracker_is_offline_and_names_beat_this_install_command()
    {
        var check = new SystemCheck([BeatThis(false), Madmom(false), Demucs(true)]);

        Assert.True(check.AnyRequiredMissing);
        Assert.Equal(SystemHealth.Offline, check.ToReported().Health);
        Assert.Equal(BeatThisBeatAnalysisStep.InstallCommand, check.ToReported().Tools[0].InstallCommand);
    }

    [Fact]
    public void The_catalog_lists_beat_this_as_the_required_beats_tool_and_madmom_as_optional()
    {
        var beats = new ExternalToolCatalog().Descriptors.Where(d => d.Capability == ToolCapabilities.Beats).ToList();

        Assert.Equal([(ExternalToolNames.BeatThis, ToolCapabilities.Beats, true), (ExternalToolNames.Madmom, ToolCapabilities.Beats, false)], beats);
    }
}
