namespace Sholto.Interface.Faceplate.Views;

/// <summary>Formats a gesture's combination as chip markers.</summary>
public sealed class ComboMarkerFormatter : IComboMarkerFormatter
{
    /// <summary>Builds the marker text for a gesture's combination name, e.g.
    /// "[[deck.shift]] + [[deck.jog]]" for "SHIFT + Jog wheel". Built straight from
    /// the gesture's own <c>With</c> list plus its owning control — never by splitting
    /// a rendered name string on "+", which breaks the moment a label contains one or
    /// a name changes.</summary>
    public string Format(IEnumerable<string> partnerIds, string ownerId) =>
        string.Join(" + ", partnerIds.Append(ownerId).Select(id => $"[[{id}]]"));
}
