using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.Minimap;

public sealed class MinimapSectionLabels : IMinimapSectionLabels
{
    public string Name(DeckSectionKind kind) => kind switch
    {
        DeckSectionKind.Intro => "INTRO",   DeckSectionKind.Build => "BUILD",
        DeckSectionKind.Drop => "DROP",     DeckSectionKind.Breakdown => "BREAK",
        DeckSectionKind.Verse => "VERSE",   DeckSectionKind.Chorus => "CHORUS",
        DeckSectionKind.Bridge => "BRIDGE", DeckSectionKind.Outro => "OUTRO",
        _ => "",
    };

    public string Fit(DeckSectionKind kind, int bars, float available, Func<string, float> measure)
    {
        string name = Name(kind);
        if (name.Length == 0) return "";
        string full = $"{name} {bars}";
        if (measure(full) <= available) return full;
        return measure(name) <= available ? name : "";
    }
}
