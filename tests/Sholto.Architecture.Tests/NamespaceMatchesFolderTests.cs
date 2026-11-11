using System.Text.RegularExpressions;

namespace Sholto.Architecture.Tests;

/// <summary>Every src file declares the namespace of its folder: the project folder name plus the dotted sub-folder path.</summary>
public sealed partial class NamespaceMatchesFolderTests
{
    private readonly SourceFiles _sources = new();

    [GeneratedRegex(@"^\s*namespace\s+([\w.]+)\s*[;{]?\s*$", RegexOptions.Multiline)]
    private static partial Regex NamespaceLine();

    [Fact]
    public void Every_source_file_declares_the_namespace_of_its_folder()
    {
        var files = _sources.All();
        Assert.NotEmpty(files);

        var offenders = Offenders(Path.Combine(_sources.RepoRoot(), "src"), files);

        Assert.True(offenders.Count == 0,
            "Files whose namespace differs from their folder:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void A_file_with_the_wrong_namespace_is_reported()
    {
        var src = Directory.CreateTempSubdirectory("ns-check").FullName;
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(src, "Sholto.Demo", "Sub")).FullName;
            var good = Path.Combine(folder, "Good.cs");
            var bad = Path.Combine(folder, "Bad.cs");
            var none = Path.Combine(folder, "None.cs");
            File.WriteAllText(good, "namespace Sholto.Demo.Sub;\npublic class Good;\n");
            File.WriteAllText(bad, "namespace Sholto.Demo;\npublic class Bad;\n");
            File.WriteAllText(none, "public class None;\n");

            var offenders = Offenders(src, [good, bad, none]);

            Assert.Equal(2, offenders.Count);
            Assert.Contains(offenders, o => o.Contains("Bad.cs") && o.Contains("Sholto.Demo.Sub"));
            Assert.Contains(offenders, o => o.Contains("None.cs"));
        }
        finally
        {
            Directory.Delete(src, recursive: true);
        }
    }

    private static List<string> Offenders(string src, IEnumerable<string> files)
    {
        var offenders = new List<string>();
        foreach (var file in files)
        {
            var parts = Path.GetRelativePath(src, file).Split(Path.DirectorySeparatorChar);
            var expected = string.Join('.', parts.SkipLast(1));
            var match = NamespaceLine().Match(File.ReadAllText(file));
            var actual = match.Success ? match.Groups[1].Value : "(none)";
            if (actual != expected)
            {
                offenders.Add($"{Path.GetRelativePath(src, file)}: declares {actual}, expected {expected}");
            }
        }

        return offenders;
    }
}
