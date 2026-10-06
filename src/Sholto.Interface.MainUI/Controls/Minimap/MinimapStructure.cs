using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.Minimap;

/// <summary>What the minimap bakes the song's structure from: the sections and phrase grid in bars, and the
/// bar grid that turns a bar into seconds (<c>FirstDownbeatSec + bar * BarPeriodSec</c>).</summary>
public sealed record MinimapStructure(
    IReadOnlyList<DeckSection> Sections,
    DeckPhraseGrid PhraseGrid,
    double FirstDownbeatSec,
    double BarPeriodSec)
{
    /// <summary>Sections and a grid to place them on; without both the strip draws no structure.</summary>
    public bool IsDrawable => Sections.Count > 0 && BarPeriodSec > 0;
}
