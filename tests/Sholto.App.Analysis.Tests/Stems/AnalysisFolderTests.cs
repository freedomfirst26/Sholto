using Sholto.App.Analysis.Stems;

namespace Sholto.App.Analysis.Tests;

public class AnalysisFolderTests
{
    [Fact]
    public void Root_is_null_before_Use()
    {
        Assert.Null(new AnalysisFolder().Root);
    }

    [Fact]
    public void Use_sets_Root()
    {
        var folder = new AnalysisFolder();

        folder.Use("/media/data/Sholto Analysis");

        Assert.Equal("/media/data/Sholto Analysis", folder.Root);
    }

    [Fact]
    public void A_second_Use_replaces_Root()
    {
        var folder = new AnalysisFolder();
        folder.Use("/a");

        folder.Use("/b");

        Assert.Equal("/b", folder.Root);
    }
}
