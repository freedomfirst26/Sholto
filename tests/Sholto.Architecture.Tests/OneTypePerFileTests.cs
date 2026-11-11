using System.Text.RegularExpressions;

namespace Sholto.Architecture.Tests;

/// <summary>CLAUDE.md rule: one top-level type per file. With file-scoped namespaces a top-level declaration is an
/// unindented line, so nested types (indented) do not count.</summary>
public sealed partial class OneTypePerFileTests
{
    private readonly SourceFiles _sources = new();

    [GeneratedRegex(@"^(?:(?:public|internal|file|sealed|abstract|static|partial|readonly|unsafe|ref)\s+)*(?:class|record|struct|interface|enum|delegate)\b")]
    private static partial Regex TopLevelDeclaration();

    [Fact]
    public void EverySourceFileHoldsAtMostOneTopLevelType()
    {
        var offending = _sources.All()
            .Select(path => (Path: path, Count: File.ReadLines(path).Count(line => TopLevelDeclaration().IsMatch(line))))
            .Where(file => file.Count > 1)
            .Select(file => $"{Path.GetRelativePath(_sources.RepoRoot(), file.Path)}: {file.Count} top-level types")
            .ToList();

        Assert.True(offending.Count == 0, string.Join(Environment.NewLine, offending));
    }

    [Fact]
    public void ScanFindsSourceFiles()
    {
        // Guards the scan: an empty list would make the rule pass without checking anything.
        Assert.NotEmpty(_sources.All());
    }
}
