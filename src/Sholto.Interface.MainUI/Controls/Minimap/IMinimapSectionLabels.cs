using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.Minimap;

/// <summary>The text on a section block in the label row.</summary>
public interface IMinimapSectionLabels
{
    /// <summary>The section's name in capitals ("DROP"); empty for a neutral section, which has none.</summary>
    string Name(DeckSectionKind kind);

    /// <summary>The longest of "NAME BARS", then "NAME", that fits in <paramref name="available"/> as
    /// <paramref name="measure"/> measures it; empty when even the name does not fit or there is none.</summary>
    string Fit(DeckSectionKind kind, int bars, float available, Func<string, float> measure);
}
