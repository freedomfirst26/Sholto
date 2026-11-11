using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Sholto.Interface.Faceplate.ViewModels;

namespace Sholto.Interface.Faceplate.Views;

public sealed class FaceplateRowVisualFactory(IFaceplatePanelPartsFactory parts, IComboMarkerFormatter comboMarkers)
    : IFaceplateRowVisualFactory
{
    private readonly IFaceplatePanelPartsFactory _parts = parts;
    private readonly IComboMarkerFormatter _comboMarkers = comboMarkers;

    /// <summary>The padding inside a bullet's hover card, and equally the margin the
    /// section eyebrow and the hairlines carry so everything in the list starts on the
    /// same left edge as the bullet text.</summary>
    private const double RowGutter = 10;

    public FaceplatePanelSection Create(IReadOnlyList<GestureRow> rows, FaceplatePanelContext context)
    {
        var children = new List<Control>();
        var indicators = new Dictionary<string, Border>();
        if (rows.Count == 0) return new FaceplatePanelSection(children, indicators);

        var label = _parts.CreateSectionLabel("EVERYTHING ELSE IT DOES");
        // 10 either side, matching the row card's padding, so the eyebrow and the
        // hairlines line up with the bullet text they belong to rather than with the
        // card's outer edge (see BuildRowVisual for why the gutter is split).
        label.Margin = new Thickness(RowGutter, 0, RowGutter, 3);
        children.Add(label);

        var first = true;
        foreach (var row in rows)
        {
            // A rule between bullets, never above the first one — the section label is
            // already the boundary there, and a rule under it would fence the label off
            // from the list it belongs to.
            if (!first) children.Add(Hairline(0.45, new Thickness(RowGutter, 14, RowGutter, 14), context));
            children.Add(BuildRowVisual(row, context, indicators));
            first = false;
        }
        return new FaceplatePanelSection(children, indicators);
    }

    /// <summary>A 1px full-width rule in the theme's border colour. Two weights: 0.55
    /// for the header rule under the summary (the same device
    /// <c>TagEditorView.axaml</c> uses), 0.45 for the quieter rules between bullets, so
    /// a list separator never reads as a section break.</summary>
    private Border Hairline(double opacity, Thickness margin, FaceplatePanelContext context) => new()
    {
        Height = 1,
        Opacity = opacity,
        Margin = margin,
        Background = context.Host.ResourceBrush("SholtoBorder", Brushes.DimGray),
        IsHitTestVisible = false,
    };

    private Control BuildRowVisual(GestureRow row, FaceplatePanelContext context, Dictionary<string, Border> indicators)
    {
        // A chord names every participant — "SHIFT + Jog wheel" — as a chip per
        // control with a plain "+" between them, so the reader never has to guess
        // which control this bullet is really about, whether it is this control's own
        // gesture or one it merely takes part in. Built from the gesture's own With
        // list plus its owner (IComboMarkerFormatter.Format), never by splitting a rendered string on
        // "+". A plain gesture (no partner) falls back to verb + part, as before —
        // that phrase names no control, so it stays plain text.
        var nameForeground = context.Host.ResourceBrush("SholtoTextBright", Brushes.White);
        Control nameControl = row.Partners.Count > 0
            ? _parts.CreateProseText(_comboMarkers.Format(row.Partners, row.OwnerControlId),
                              13, FontWeight.SemiBold, nameForeground, lineHeight: 18, context)
            : new TextBlock
              {
                  Text = row.Part is null ? Capitalize(row.Verb) : $"{Capitalize(row.Verb)} — {row.Part}",
                  FontSize = 13,
                  FontWeight = FontWeight.SemiBold,
                  LineHeight = 18,
                  TextWrapping = TextWrapping.Wrap,
                  Foreground = nameForeground,
              };
        ((Control)nameControl).Opacity = 0.86;

        var stack = new StackPanel();
        // A control with two rows on the same layer (a pad, across pages) has nothing
        // else to tell them apart by, since a page switch names no partner — so a
        // non-plain layer is tagged. "Plain" itself gets NO tag: everything is plain
        // unless something overrides it, so a PLAIN label on most rows would be noise
        // that dilutes the tags that actually mean something (SHIFT, HOT CUE page, …).
        // Skipping the child entirely — not just hiding it — is what keeps the
        // untagged rows from showing a gap where the tag would have sat.
        // The tag now wears the app's own chip form (TagEditorView) rather than floating
        // as a coloured word: a filled pill in SholtoAccentBg, which is the same brush
        // the track list, the search overlay and the crate picker all use for "this one
        // is picked". It is the only colour in the panel, and it only appears when
        // there is a modifier to name.
        // …but not when the chord above it already names the modifier: a bullet reading
        // SHIFT / "SHIFT + JOG WHEEL" says the same word twice in two different forms
        // one line apart, and the reader has to check whether the second one means
        // something new. The tag stays for a layered gesture whose modifier is not in
        // the chord — the pad pages, where the page switch names no partner.
        var modifier = context.ViewModel.Doc.Layers.FirstOrDefault(l => l.Id == row.Layer)?.Modifier;
        if (row.Layer != "plain" && (modifier is null || !row.Partners.Contains(modifier)))
        {
            stack.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(7, 1, 7, 2),
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = context.Host.ResourceBrush("SholtoAccentBg", Brushes.Transparent),
                Child = new TextBlock
                {
                    Text = context.LayerNameOf(row.Layer).ToUpperInvariant(),
                    FontSize = 11,
                    FontWeight = FontWeight.Bold,
                    LetterSpacing = 0.8,
                    LineHeight = 13,
                    Foreground = context.Host.ResourceBrush("SholtoAccent", Brushes.Orange),
                },
            });
        }
        stack.Children.Add(nameControl);

        // Same 13px as the gesture name above it. Weight and opacity do the separating,
        // which keeps the whole panel on four sizes: 18 title, 16 answer, 13 body,
        // 11 eyebrow and cap.
        var result = _parts.CreateProseText(row.Result, 13, FontWeight.Normal,
            context.Host.ResourceBrush("SholtoTextBright", Brushes.White), lineHeight: 18, context);
        // 6, not 4: the gesture name above is 13px on an 18px line, so 4 put the result
        // closer to its own name than the name's two wrapped lines are to each other and
        // the pair read as one squashed block. A name carrying chips sits on a 27px
        // line, which already has more air under it, so the gap is taken off there to
        // keep the name→result step the same distance in both kinds of bullet.
        result.Margin = new Thickness(0, nameControl is TextBlock { LineHeight: > 18 } ? 2 : 6, 0, 0);
        result.Opacity = 0.56;
        if (!row.Used)
        {
            // Same rule as the headline: no red, no accent — italic and a grey step
            // down, which is the panel's one and only way of saying "not this one".
            result.FontStyle = FontStyle.Italic;
            result.Foreground = context.Host.ResourceBrush("SholtoTextMuted", Brushes.Gray);
            result.Opacity = 0.70;
        }
        stack.Children.Add(result);

        // At rest a bullet has no chrome at all — nine bordered boxes in a column is
        // what made this read as a bolted-on document viewer rather than part of
        // Sholto, and the hairline rhythm above turns those nine objects into one
        // list. The box moves to hover, where it MEANS something: it appears exactly
        // when the pointer is on a row, which is exactly when the matching controls
        // light up on the board.
        // The card sits INSIDE the panel's gutter, not across it. It used to carry a
        // -10 horizontal margin meant to bleed into that gutter, but the ScrollViewer
        // clips to its viewport, so the bleed was simply cut off: the highlight ended
        // flush with the first letter of its own text on one side and with the panel's
        // border on the other, its rounded corners sliced away, reading as a block
        // bleeding off the edge rather than a card lifting off the page. The gutter is
        // now split — 12 of panel margin outside the card, 10 of padding inside it —
        // so nothing is clipped and the text keeps the same 22 the title and summary
        // sit on (see the ScrollViewer's margin in FaceplateOverlay.axaml).
        var border = new Border
        {
            Padding = new Thickness(RowGutter, 8),
            CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent,
            Child = stack,
        };
        // Set on attach, as ControlChip does: a Cursor needs the platform's cursor factory,
        // which an unattached control (as in the tests) does not have. A row is always
        // attached before the pointer can reach it.
        border.AttachedToVisualTree += (_, _) => border.Cursor ??= new Cursor(StandardCursorType.Hand);
        border.Transitions = new Avalonia.Animation.Transitions
        {
            new Avalonia.Animation.BrushTransition
            {
                Property = Border.BackgroundProperty,
                Duration = TimeSpan.FromMilliseconds(120),
            },
        };
        border.PointerEntered += (_, _) =>
        {
            border.Background = context.Host.ResourceBrush("SholtoAccentBg", Brushes.Transparent);
            context.ViewModel.HoverRow(row.GestureId);
        };
        border.PointerExited += (_, _) =>
        {
            border.Background = Brushes.Transparent;
            context.ViewModel.HoverRow(null);
        };

        var indicator = _parts.CreateIndicator(context);
        indicators[row.GestureId] = indicator;
        return _parts.CreateIndicatedRow(indicator, border);
    }

    private string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
