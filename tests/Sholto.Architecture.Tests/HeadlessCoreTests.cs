namespace Sholto.Architecture.Tests;

/// <summary>C55 dependency rule for the headless core: Sholto.App and every Sholto.App.* project reference no
/// Avalonia and no Sholto.Interface.*, so the app runs with any interface (or none).</summary>
public sealed class HeadlessCoreTests
{
    private readonly AssemblyReferences _references = new();

    [Theory]
    [InlineData("Sholto.App")]
    [InlineData("Sholto.App.Analysis")]
    [InlineData("Sholto.App.Audio")]
    [InlineData("Sholto.App.Dsp")]
    [InlineData("Sholto.App.ExternalTools")]
    [InlineData("Sholto.App.Library")]
    [InlineData("Sholto.App.Settings")]
    [InlineData("Sholto.App.Storage")]
    public void AppAssemblyReferencesNoAvaloniaAndNoInterface(string assembly)
    {
        var offending = _references.Of(assembly)
            .Where(reference => _references.IsUnder(reference, "Avalonia")
                || _references.IsUnder(reference, "Sholto.Interface"))
            .ToList();

        Assert.True(offending.Count == 0, $"{assembly} must not reference: {string.Join(", ", offending)}");
    }

    [Fact]
    public void EveryBuiltAppSubProjectIsCoveredByTheRule()
    {
        var offending = _references.AppSubProjects()
            .Append("Sholto.App")
            .SelectMany(assembly => _references.Of(assembly)
                .Where(reference => _references.IsUnder(reference, "Avalonia")
                    || _references.IsUnder(reference, "Sholto.Interface"))
                .Select(reference => $"{assembly} -> {reference}"))
            .ToList();

        Assert.True(offending.Count == 0, string.Join(Environment.NewLine, offending));
    }

    [Fact]
    public void AppSubProjectsAreFoundNextToTheTests()
    {
        // Guards the discovery above: an empty list would make the rule pass without checking anything.
        Assert.Contains("Sholto.App.Audio", _references.AppSubProjects());
        Assert.Contains("Sholto.App.Storage", _references.AppSubProjects());
    }
}
