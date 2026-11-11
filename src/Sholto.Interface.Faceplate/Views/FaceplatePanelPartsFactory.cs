using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Sholto.Interface.Faceplate.Views;

public sealed class FaceplatePanelPartsFactory(IProseInlinesFactory prose) : IFaceplatePanelPartsFactory
{
    private readonly IProseInlinesFactory _prose = prose;

    /// <summary>The line height a paragraph needs once it contains a key cap: the cap's
    /// own height plus the leading that keeps a wrapped line from touching it.</summary>
    private const double ChipLineHeight = 24;

    /// <summary>Builds a prose <see cref="TextBlock"/> from marker text, wiring every
    /// chip it contains to the same hover and selection paths a drawing shape uses:
    /// hover lights the named control, a click selects it exactly as clicking its
    /// shape would (see <see cref="FaceplateBoard.OnSelectionChanged"/>). Hover lights exactly the
    /// control the chip names — precise, unlike a hovered row, which highlights every
    /// participant in a combination at once. A click goes through the exact same path a
    /// drawing shape's click uses, so an in-flight blink is cancelled the same way and only
    /// one control is ever held lit.</summary>
    public TextBlock CreateProseText(string markerText, double fontSize, FontWeight weight,
                                     IBrush foreground, double lineHeight, FaceplatePanelContext context)
    {
        var tb = new TextBlock
        {
            FontSize = fontSize,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            Foreground = foreground,
        };
        tb.Inlines = _prose.Create(markerText, context.ViewModel.Doc, context.ChipDeck,
            (id, deck) => context.Board.SetHover(id, deck, true),
            (id, deck) => context.Board.SetHover(id, deck, false),
            (id, deck) => context.ViewModel.Select(id, deck));
        // A chip is a bordered, padded box roughly 19px tall, so a line height chosen
        // for 13px or 16px TEXT is shorter than the line it now has to hold. Avalonia
        // does not grow the line to fit: it lays the lines out at the height it was
        // given and the chips spill into their neighbours, which is what made a
        // chip-bearing sentence read as two sentences printed on top of each other.
        // Prose with chips therefore gets a line tall enough for the cap, and prose
        // without keeps the tighter rhythm it was tuned for.
        tb.LineHeight = tb.Inlines.OfType<InlineUIContainer>().Any()
            ? Math.Max(lineHeight, ChipLineHeight)
            : lineHeight;
        return tb;
    }

    /// <summary>The thin accent bar that marks "this is the row a live gesture just
    /// fired" — the same accent colour the row tags and the new layer badge wear
    /// (never amber; amber is the drawing's alone). Built invisible and toggled by the
    /// overlay's ApplyActiveRow, not rebuilt per gesture: a knob can fire many
    /// times a second and only the opacity needs to move.</summary>
    public Border CreateIndicator(FaceplatePanelContext context) => new()
    {
        Width = 3,
        CornerRadius = new CornerRadius(2),
        Margin = new Thickness(0, 1, 8, 1),
        Background = context.Host.ResourceBrush("SholtoAccent", Brushes.Orange),
        Opacity = 0,
        IsHitTestVisible = false,
    };

    /// <summary>Lays the indicator bar and a row's own content side by side without
    /// disturbing that content's existing hover/click wiring — the indicator is a
    /// sibling, not a wrapper around it.</summary>
    public Grid CreateIndicatedRow(Border indicator, Control content)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(indicator, 0);
        Grid.SetColumn(content, 1);
        grid.Children.Add(indicator);
        grid.Children.Add(content);
        return grid;
    }

    /// <summary>The small all-caps eyebrow over a section. Carries no styling of its
    /// own: size, weight, tracking, opacity and colour all come from the single
    /// <c>TextBlock.eyebrow</c> style in the .axaml, which the deck eyebrow above the
    /// title wears too. See that style for why the colour is what it is.</summary>
    public TextBlock CreateSectionLabel(string text) => new()
    {
        Text = text,
        Classes = { "eyebrow" },
    };
}
