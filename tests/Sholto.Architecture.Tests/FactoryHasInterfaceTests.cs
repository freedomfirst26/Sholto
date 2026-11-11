using System.Text.RegularExpressions;

namespace Sholto.Architecture.Tests;

/// <summary>CLAUDE.md: a factory is an XFactory behind an IXFactory interface. Every src class named XFactory must list
/// IXFactory in its base list, unless it is in <see cref="Allowed"/>.</summary>
public sealed partial class FactoryHasInterfaceTests
{
    // FROZEN: today's real offenders, sorted. It may only shrink: remove an entry when its class gains its interface
    // or is renamed or deleted (the stale-entry test fails until you do). Never add one.
    private static readonly string[] Allowed =
    [];

    private readonly SourceFiles _sources = new();

    // A top-level class is an unindented line (file-scoped namespaces).
    [GeneratedRegex(@"^(?:(?:public|internal|sealed|abstract|static|partial)\s+)*class\s+(?<name>[A-Za-z_]\w*Factory)\b")]
    private static partial Regex FactoryClass();

    [Fact]
    public void EveryFactoryClassImplementsItsInterfaceOutsideTheAllowlist()
    {
        var offenders = Factories().Where(f => !f.HasInterface && !Allowed.Contains(f.Name)).Select(f => f.Description).ToList();

        Assert.True(offenders.Count == 0,
            "Every XFactory class implements IXFactory; these do not:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryAllowlistEntryStillExists()
    {
        var offending = Factories().Where(f => !f.HasInterface).Select(f => f.Name).ToHashSet();
        var stale = Allowed.Where(entry => !offending.Contains(entry)).ToList();

        Assert.True(stale.Count == 0,
            "Allowlist entries now implement their interface or no longer exist; remove them so the list only shrinks:\n"
            + string.Join("\n", stale));
    }

    private List<(string Name, bool HasInterface, string Description)> Factories() =>
        _sources.All()
            .SelectMany(path =>
            {
                var text = File.ReadAllText(path);
                return text.Split('\n')
                    .Select(line => FactoryClass().Match(line))
                    .Where(match => match.Success)
                    .Select(match => match.Groups["name"].Value)
                    .Select(name => (
                        Name: name,
                        HasInterface: Regex.IsMatch(text, @"[:,]\s*I" + name + @"\b"),
                        Description: $"{name} ({Path.GetRelativePath(_sources.RepoRoot(), path)})"));
            })
            .ToList();
}
