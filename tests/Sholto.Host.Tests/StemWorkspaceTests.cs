using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.ToolBoundary;
using Xunit;

namespace Sholto.Host.Tests;

public class StemWorkspaceTests
{
    private readonly AnalysisFolder _folder = new();
    private readonly ExternalToolStackFactory _toolStacks;

    public StemWorkspaceTests() =>
        _toolStacks = new(
            new ExternalToolFinderFactory(),
            new BeatThisBeatAnalysisStepFactory(new ToolOutcomeFactory(), new DetectedBeatsFactory(), Path.GetTempPath()),
            new MadmomBeatAnalysisStepFactory(new ToolOutcomeFactory(), new DetectedBeatsFactory()),
            new ToolOutcomeFactory(),
            Microsoft.Extensions.Options.Options.Create(new StemSeparationOptions()),
            _folder, new RecordingLogFactory());

    [Fact]
    public void NullRoot_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _toolStacks.StemWorkspaceFor("/Music/a.mp3"));
    }

    [Fact]
    public void Used_RootGivesRootStemsHash()
    {
        _folder.Use("/analysis/a");
        Assert.Equal(Path.Combine("/analysis/a", "stems", _toolStacks.HashPath("/Music/a.mp3")), _toolStacks.StemWorkspaceFor("/Music/a.mp3"));
    }

    [Fact]
    public void AfterUseOfAnotherRoot_SameInputFollowsTheNewRoot()
    {
        _folder.Use("/analysis/a");
        _folder.Use("/analysis/b");
        Assert.Equal(Path.Combine("/analysis/b", "stems", _toolStacks.HashPath("/Music/a.mp3")), _toolStacks.StemWorkspaceFor("/Music/a.mp3"));
    }
}
