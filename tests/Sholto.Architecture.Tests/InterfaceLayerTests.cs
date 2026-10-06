namespace Sholto.Architecture.Tests;

/// <summary>C55 dependency rule for the interfaces: every Sholto.Interface.*, MainUI included, talks to the app only
/// through Sholto.Data, and the non-UI ones reference no Avalonia. Only Sholto.Host (the exe, assembly "Sholto" and
/// the composition root) may reference App and Interfaces together.</summary>
public sealed class InterfaceLayerTests
{
    private readonly AssemblyReferences _references = new();

    [Theory]
    [InlineData("Sholto.Interface.Controller")]
    [InlineData("Sholto.Interface.Controller.Mappings")]
    [InlineData("Sholto.Interface.Faceplate")]
    [InlineData("Sholto.Interface.Faceplate.Devices")]
    [InlineData("Sholto.Interface.Keyboard")]
    [InlineData("Sholto.Interface.MainUI")]
    public void InterfaceAssemblyReferencesNoApp(string assembly)
    {
        var offending = _references.Of(assembly)
            .Where(reference => _references.IsUnder(reference, "Sholto.App"))
            .ToList();

        Assert.True(offending.Count == 0, $"{assembly} must not reference: {string.Join(", ", offending)}");
    }

    // Controller and its mappings are not UI. Faceplate and Faceplate.Devices are (they draw the device).
    // Exception to the plan's wording: Keyboard also references Avalonia, for the Avalonia.Input.Key and
    // KeyModifiers types that its KeyboardEvent / IKeyboard / recogniser are written in terms of.
    [Theory]
    [InlineData("Sholto.Interface.Controller")]
    [InlineData("Sholto.Interface.Controller.Mappings")]
    public void NonUiInterfaceAssemblyReferencesNoAvalonia(string assembly)
    {
        var offending = _references.Of(assembly)
            .Where(reference => _references.IsUnder(reference, "Avalonia"))
            .ToList();

        Assert.True(offending.Count == 0, $"{assembly} must not reference: {string.Join(", ", offending)}");
    }

    // C55 F11: Sholto.Interface.Bench is the headless interface (scripted scenario -> Commands on
    // Sholto.Data). The window-driving code lives in the MainUI harness tool, so Bench references no
    // UI toolkit and not MainUI. MainUI is the library Sholto.Interface.MainUI; "Sholto" is the bootstrapper
    // exe (Sholto.Host), so Bench must reference neither.
    [Fact]
    public void BenchReferencesNoAvalonia()
    {
        var offending = _references.Of("Sholto.Interface.Bench")
            .Where(reference => _references.IsUnder(reference, "Avalonia"))
            .ToList();

        Assert.True(offending.Count == 0, $"Sholto.Interface.Bench must not reference: {string.Join(", ", offending)}");
    }

    [Fact]
    public void BenchReferencesNoMainUI()
    {
        var offending = _references.Of("Sholto.Interface.Bench")
            .Where(reference => reference == "Sholto" || _references.IsUnder(reference, "Sholto.Interface.MainUI"))
            .ToList();

        Assert.True(offending.Count == 0, $"Sholto.Interface.Bench must not reference: {string.Join(", ", offending)}");
    }
}
