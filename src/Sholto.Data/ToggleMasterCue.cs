namespace Sholto.Data;

/// <summary>Flip MASTER CUE.</summary>
public readonly record struct ToggleMasterCue(Origin Origin) : ICommand;
