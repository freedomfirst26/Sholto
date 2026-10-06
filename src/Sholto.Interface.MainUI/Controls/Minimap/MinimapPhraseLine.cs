namespace Sholto.Interface.MainUI.Controls.Minimap;

/// <summary>A phrase line: the bar it sits on and how strong it is.</summary>
public readonly record struct MinimapPhraseLine(int Bar, MinimapPhraseLineWeight Weight);
