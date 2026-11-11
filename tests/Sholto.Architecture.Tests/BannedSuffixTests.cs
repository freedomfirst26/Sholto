using System.Text.RegularExpressions;

namespace Sholto.Architecture.Tests;

/// <summary>CLAUDE.md: a type that constructs things is a Factory or Builder, never an XParser, XMapper, XConverter,
/// XMaker, XCreator, XProvider or XJson. Every other src type whose name ends in one of those suffixes must be in
/// <see cref="Allowed"/>.</summary>
public sealed partial class BannedSuffixTests
{
    private static readonly string[] BannedSuffixes = ["Parser", "Mapper", "Converter", "Maker", "Creator", "Provider", "Json"];

    // FROZEN: today's real hits, sorted. It may only shrink: remove an entry when its type is renamed or deleted
    // (the stale-entry test fails until you do). Never add one.
    private static readonly string[] Allowed =
    [
        "AnalysisProvider",
        "CachingAnalysisProvider",
        "DbAnalysisProvider",
        "IAnalysisProvider",
        "IVarispeedProvider",
        "LowercaseGuidConverter",
        "ScratchDataProvider",
        "StagedAnalysisProvider",
        "StemMixDataProvider",
    ];

    private readonly SourceFiles _sources = new();

    // A top-level declaration is an unindented line (file-scoped namespaces); the name is the identifier after the keyword.
    [GeneratedRegex(@"^(?:(?:public|internal|file|sealed|abstract|static|partial|readonly|unsafe|ref)\s+)*(?:class|record\s+struct|record\s+class|record|struct|interface|enum)\s+(?<name>[A-Za-z_]\w*)")]
    private static partial Regex TopLevelDeclaration();

    [Fact]
    public void NoTypeEndsInABannedSuffixOutsideTheAllowlist()
    {
        var offenders = Found().Where(hit => !Allowed.Contains(hit.Name)).Select(hit => hit.Description).ToList();

        Assert.True(offenders.Count == 0,
            "Constructing types are named XFactory or XBuilder; these names end in a banned suffix (" + string.Join(", ", BannedSuffixes) + "):\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryAllowlistEntryStillExists()
    {
        var found = Found().Select(hit => hit.Name).ToHashSet();
        var stale = Allowed.Where(entry => !found.Contains(entry)).ToList();

        Assert.True(stale.Count == 0,
            "Allowlist entries no longer exist; remove them so the list only shrinks:\n" + string.Join("\n", stale));
    }

    private List<(string Name, string Description)> Found() =>
        _sources.All()
            .SelectMany(path => File.ReadLines(path)
                .Select(line => TopLevelDeclaration().Match(line))
                .Where(match => match.Success)
                .Select(match => match.Groups["name"].Value)
                .Where(name => BannedSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal)))
                .Select(name => (Name: name, Description: $"{name} ({Path.GetRelativePath(_sources.RepoRoot(), path)})")))
            .ToList();
}
