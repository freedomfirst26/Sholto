using Sholto.Data;

namespace Sholto.App.ExternalTools.Tests;

/// <summary>The boot-time tool probe as the event the interfaces are told.</summary>
public class SystemCheckTests
{
    [Fact]
    public void ToReported_carries_every_field_the_madmom_install_command_and_the_degraded_health()
    {
        var check = new SystemCheck([
            new ToolPresence(ExternalToolNames.Madmom, ToolCapabilities.Beats, true, "/usr/bin/madmom"),
            new ToolPresence(ExternalToolNames.Demucs, ToolCapabilities.Stems, false, null)]);

        var reported = check.ToReported();

        Assert.Equal(SystemHealth.Degraded, reported.Health);
        Assert.Equal(
            [
                new ToolStatus(ExternalToolNames.Madmom, ToolCapabilities.Beats, true, "/usr/bin/madmom", MadmomBeatAnalysisStep.InstallCommand),
                new ToolStatus(ExternalToolNames.Demucs, ToolCapabilities.Stems, false, null, null),
            ],
            reported.Tools);
    }
}
