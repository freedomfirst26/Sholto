using System.Text.RegularExpressions;
using Avalonia.Media;
using Avalonia.Controls.Documents;
using Sholto.Faceplate.Model;

namespace Sholto.Faceplate.Views;

/// <summary>Turns a plain string into an <see cref="InlineCollection"/> mixing plain
/// text runs and <see cref="ControlChip"/>s, splitting on explicit
/// <c>[[control.id]]</c> markers rather than guessing control names out of prose.
/// <para>Guessing is what breaks: "cue" appears in ordinary sentences, and this unit
/// has two different CUE buttons. An explicit id is unambiguous and never chips the
/// wrong word.</para></summary>
public static partial class ProseWithChips
{
    [GeneratedRegex(@"\[\[([^\[\]]+)\]\]")]
    private static partial Regex MarkerRegex();

    /// <summary>Builds the inlines for <paramref name="text"/>. A marker naming a
    /// control absent from <paramref name="doc"/> renders as its own raw text
    /// (brackets included) rather than throwing — a typo in the guide JSON must never
    /// blank the panel. A "[[" with no matching "]]" is left as plain text too, since
    /// the marker regex simply never matches it.</summary>
    public static InlineCollection Build(
        string text,
        FaceplateDoc doc,
        int deck,
        Action<string, int>? onHover = null,
        Action<string, int>? onHoverEnd = null,
        Action<string, int>? onActivate = null)
    {
        var inlines = new InlineCollection();
        var runs = new List<Run>();
        var pos = 0;
        var chipped = false;

        void AddText(string s)
        {
            var run = new Run(s);
            runs.Add(run);
            inlines.Add(run);
        }

        foreach (Match m in MarkerRegex().Matches(text))
        {
            if (m.Index > pos) AddText(text[pos..m.Index]);

            var id = m.Groups[1].Value;
            var control = doc.Controls.FirstOrDefault(c => c.Id == id);
            if (control is null)
            {
                AddText(m.Value);
            }
            else
            {
                // A per-deck control's chip inherits the panel's current deck; a
                // global control has no deck at all.
                var chipDeck = control.Scope == "per-deck" ? deck : -1;
                var chip = new ControlChip(control.Id, chipDeck, control.Label);
                if (onHover is not null) chip.Hovered += onHover;
                if (onActivate is not null) chip.Activated += onActivate;
                if (onHoverEnd is not null)
                {
                    var (capturedId, capturedDeck) = (control.Id, chipDeck);
                    chip.PointerExited += (_, _) => onHoverEnd(capturedId, capturedDeck);
                }
                // Centred, not baseline-aligned. A key cap is a bordered, padded box
                // about 19px tall, and sitting it ON the baseline puts its middle some
                // 9px above the middle of the words beside it — which is why the plain
                // "+" joining SHIFT to JOG WHEEL read as having fallen off the line.
                // Centring the caps AND the words in the same line box lines the two up
                // whatever the cap's label, so a one-word cap and a four-word one behave
                // identically.
                inlines.Add(new InlineUIContainer(chip)
                {
                    BaselineAlignment = BaselineAlignment.Center,
                });
                chipped = true;
            }
            pos = m.Index + m.Length;
        }
        if (pos < text.Length) AddText(text[pos..]);

        // Only where there is a cap to line up with: plain prose keeps ordinary
        // baseline layout, so nothing moves in the sentences that have no chips.
        if (chipped)
            foreach (var run in runs)
                run.BaselineAlignment = BaselineAlignment.Center;

        return inlines;
    }

    /// <summary>Builds the marker text for a gesture's combination name, e.g.
    /// "[[deck.shift]] + [[deck.jog]]" for "SHIFT + Jog wheel". Built straight from
    /// the gesture's own <c>With</c> list plus its owning control — never by splitting
    /// a rendered name string on "+", which breaks the moment a label contains one or
    /// a name changes.</summary>
    public static string ComboMarkers(IEnumerable<string> partnerIds, string ownerId) =>
        string.Join(" + ", partnerIds.Append(ownerId).Select(id => $"[[{id}]]"));
}
