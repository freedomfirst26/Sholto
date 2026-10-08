namespace Sholto.Data;

/// <summary>The browse knob turned by <see cref="Delta"/> clicks.</summary>
public readonly record struct RotateBrowse(int Delta, Origin Origin) : ICommand;
