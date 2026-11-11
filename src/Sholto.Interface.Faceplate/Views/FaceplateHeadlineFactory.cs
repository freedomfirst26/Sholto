using Avalonia.Media;
using Sholto.Interface.Faceplate.ViewModels;

namespace Sholto.Interface.Faceplate.Views;

/// <summary>Builds the single headline line: "what does a plain press do?".</summary>
public sealed class FaceplateHeadlineFactory(IFaceplatePanelPartsFactory parts) : IFaceplateHeadlineFactory
{
    private readonly IFaceplatePanelPartsFactory _parts = parts;

    public FaceplatePanelSection Create(GestureRow headline, FaceplatePanelContext context)
    {
        var label = _parts.CreateSectionLabel($"IF YOU JUST {headline.Verb.ToUpperInvariant()} IT");

        // A plain press that does nothing (transport CUE, BEAT SYNC) says so here,
        // plainly — that is the honest and useful answer, not a reason to hide it. It
        // is said the way the DRAWING says it, by withholding rather than by warning:
        // the board gives an unimplemented control no colour at all, so the panel gives
        // the sentence no weight — one grey step down, italic, no accent and no red.
        // Italic carries exactly one meaning anywhere in this panel, "this is a
        // negative statement", which is what makes the state findable in a glance
        // without a colour shouting at anyone.
        var head = _parts.CreateProseText(headline.Result, 16, FontWeight.SemiBold,
            context.Host.ResourceBrush("SholtoTextBright", Brushes.White), lineHeight: 22, context);
        if (!headline.Used)
        {
            // Same 16px as a working answer, deliberately. The size says "this is the
            // answer to the question the eyebrow just asked"; the italic and the 0.62
            // say the answer is a negative. Dropping the size as well made the negative
            // headline merge with the summary above it into one continuous grey slab,
            // and the reader could no longer tell which of the two was the answer.
            head.FontWeight = FontWeight.Normal;
            head.FontStyle = FontStyle.Italic;
            head.Opacity = 0.62;
        }
        var indicator = _parts.CreateIndicator(context);
        return new FaceplatePanelSection(
            [label, _parts.CreateIndicatedRow(indicator, head)],
            new Dictionary<string, Avalonia.Controls.Border> { [headline.GestureId] = indicator });
    }
}
