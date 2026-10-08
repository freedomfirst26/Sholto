namespace Sholto.Data;

/// <summary>Re-analyse the highlighted library track (browse knob held).</summary>
public readonly record struct ReanalyzeSelected(Origin Origin) : ICommand;
