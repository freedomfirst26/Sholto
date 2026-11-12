using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.Host.Tests;

/// <summary>Which beat tracker the app runs, decided by which one is installed; built as
/// <c>AppStackFactory</c> builds it, with only the finder replaced.</summary>
public class BeatStepSelectionTests
{
    private ExternalToolStack Build(params string[] installed) =>
        new ExternalToolStackFactory(
            new FixedToolFinderFactory(installed),
            new BeatThisBeatAnalysisStepFactory(new ToolOutcomeFactory(), new DetectedBeatsFactory(), Path.GetTempPath()),
            new MadmomBeatAnalysisStepFactory(new ToolOutcomeFactory(), new DetectedBeatsFactory()),
            new ToolOutcomeFactory(),
            Options.Create(new StemSeparationOptions()),
            new AnalysisFolder(), new RecordingLogFactory()).Build();

    [Fact]
    public void Beat_this_is_used_when_it_is_installed_even_beside_madmom()
    {
        Assert.IsType<BeatThisBeatAnalysisStep>(Build(ExternalToolNames.BeatThis).Beats);
        Assert.IsType<BeatThisBeatAnalysisStep>(Build(ExternalToolNames.BeatThis, ExternalToolNames.Madmom).Beats);
    }

    [Fact]
    public void Madmom_is_used_only_when_it_is_the_one_installed()
    {
        var stack = Build(ExternalToolNames.Madmom);

        Assert.IsType<MadmomBeatAnalysisStep>(stack.Beats);
        Assert.True(stack.Beats.IsAvailable);
    }

    [Fact]
    public async Task With_neither_installed_analysis_fails_naming_the_beat_this_install_command()
    {
        var stack = Build();

        Assert.IsType<BeatThisBeatAnalysisStep>(stack.Beats);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => stack.Beats.AnalyzeAsync("/music/track.mp3"));
        Assert.Contains(BeatThisBeatAnalysisStep.InstallCommand, ex.Message);
        Assert.True(stack.Check.AnyRequiredMissing);
    }
}
