namespace Sholto.Data;

/// <summary>The user picked a theme; the App remembers its name for the next launch.</summary>
public readonly record struct ChooseTheme(string Name, Origin Origin) : ICommand;
