using System.Text.RegularExpressions;

namespace Sholto.App.ExternalTools.Tests;

/// <summary>The install command the app shows for Beat This! is the one the installers run:
/// built from the pins in <c>sholto-deps.sh</c>, so bumping one without the other fails here.</summary>
public class BeatThisInstallCommandTests
{
    [Fact]
    public void The_install_command_carries_the_python_spec_pins_and_index_of_sholto_deps()
    {
        var deps = File.ReadAllText(Path.Combine(RepoRoot(), "sholto-deps.sh"));

        var expected = string.Join(' ',
            "uv tool install --python", Value(deps, "SHOLTO_PYTHON"),
            Value(deps, "SHOLTO_BEATTHIS_SPEC"),
            Regex.Match(deps, @"^SHOLTO_BEATTHIS_WITH=\((.*)\)$", RegexOptions.Multiline).Groups[1].Value,
            "--index", Value(deps, "SHOLTO_BEATTHIS_INDEX"));

        Assert.Equal(expected, BeatThisBeatAnalysisStep.InstallCommand);
    }

    private string Value(string deps, string name)
    {
        var match = Regex.Match(deps, $"^{name}=\"(.*)\"$", RegexOptions.Multiline);
        Assert.True(match.Success, $"{name} not found in sholto-deps.sh");
        return match.Groups[1].Value;
    }

    private string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sholto.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Sholto.slnx not found above the test binary.");
    }
}
