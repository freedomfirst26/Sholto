using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Sholto.Faceplate.Views;

/// <summary>A control name rendered as a key cap — a small raised box, not a code
/// span. Markdown's backtick box is flat because code is flat; these name physical
/// objects on the controller, so a slight raise reads truer: a rounded rect, a
/// theme border, a fill one step lighter than the card it sits on, and a 1px darker
/// line along the bottom edge suggesting depth.
/// <para>Text is small caps and letter-spaced rather than monospace — monospace says
/// "code"; letter-spaced caps say "this is printed on the hardware", which is what
/// HOT CUE actually is on the unit. Avalonia has no font-variant small-caps, so this
/// approximates it with uppercase text at a reduced size, which reads the same way.
/// </para>
/// <para><b>Never amber.</b> Amber is reserved for the device drawing; this derives
/// its colour entirely from the theme's <c>SholtoSurfaceRaised</c> / <c>SholtoBorder</c>
/// / <c>SholtoTextBright</c> resources, so it reads correctly in every theme.</para></summary>
public sealed class ControlChip : Border
{
    /// <summary>The control this chip names.</summary>
    public string ControlId { get; }

    /// <summary>0 or 1 for a per-deck control's chip, -1 for a global one.</summary>
    public int Deck { get; }

    /// <summary>The label printed on the cap.</summary>
    public string Label { get; }

    /// <summary>The pointer entered the chip. The overlay lights the named control on
    /// the drawing, exactly as if the pointer had entered its shape — turning it back
    /// off is the chip's own (inherited) <see cref="InputElement.PointerExited"/>.
    /// </summary>
    public event Action<string, int>? Hovered;

    /// <summary>The chip was clicked. The overlay selects the named control exactly as
    /// clicking its shape does — the same path, so an in-flight blink on some other
    /// control is cancelled the same way and only one control is ever held lit.
    /// </summary>
    public event Action<string, int>? Activated;

    private readonly Border _inner;
    private readonly TextBlock _text;

    public ControlChip(string controlId, int deck, string label)
    {
        ControlId = controlId;
        Deck = deck;
        Label = label;

        CornerRadius = new CornerRadius(4);
        BorderThickness = new Thickness(1);
        VerticalAlignment = VerticalAlignment.Center;

        _text = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            LetterSpacing = 1.1,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _inner = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 1),
            // Inset from the bottom edge only, so the outer shape's darker fill peeks
            // through as a 1px line — the "raised key cap" edge the brief asks for,
            // without a second overlapping shape to keep in sync.
            Margin = new Thickness(0, 0, 0, 1),
            Child = _text,
        };
        Child = _inner;

        PointerEntered += (_, _) =>
        {
            SetRaised(true);
            Hovered?.Invoke(ControlId, Deck);
        };
        PointerExited += (_, _) => SetRaised(false);
        PointerPressed += (_, e) =>
        {
            e.Handled = true;   // a chip click must not fall through to the panel/backdrop
            Activated?.Invoke(ControlId, Deck);
        };

        ApplyTheme();
        ActualThemeVariantChanged += (_, _) => ApplyTheme();
        // The palette is swapped by rewriting Window.Resources, which raises this and
        // nothing else — without it a chip keeps the colours of whatever theme was
        // live when it was built.
        ResourcesChanged += (_, _) => ApplyTheme();
    }

    /// <summary>Sets the hand cursor once actually mounted in a live visual tree.
    /// <see cref="Cursor"/> needs a platform cursor factory, which only exists once
    /// Avalonia has a running application — deferring this (rather than setting it in
    /// the constructor) is what lets <see cref="ProseWithChips.Build"/> be unit-tested
    /// with no application at all.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Cursor ??= new Cursor(StandardCursorType.Hand);
        ApplyTheme();   // the window is only reachable now, so this is the first real palette
    }

    /// <summary>Lifts the cap under the pointer. This is not decoration: the hint line
    /// at the bottom of the guide teaches the reader that a small bordered cap of
    /// exactly this shape is a LABEL — the Esc cap there is
    /// <c>IsHitTestVisible="False"</c> — and then the panel asks them to click one.
    /// Raising the cap and brightening its rim is the cheapest available "this one is a
    /// key you can actually press", and it costs no new colour: the rim borrows
    /// <c>SholtoTextBright</c>, which is already the chip's own text colour.</summary>
    private void SetRaised(bool raised)
    {
        _inner.Margin = new Thickness(0, 0, 0, raised ? 2 : 1);
        BorderBrush = raised
            ? ResourceBrush("SholtoTextBright", Brushes.White)
            : ResourceBrush("SholtoBorder", Brushes.DimGray);
    }

    private void ApplyTheme()
    {
        var borderBrush = ResourceBrush("SholtoBorder", Brushes.DimGray);
        var surface = ResourceColor("SholtoSurfaceRaised", Color.FromRgb(0x1F, 0x1A, 0x3D));
        var textBright = ResourceBrush("SholtoTextBright", Brushes.White);

        BorderBrush = borderBrush;
        // The outer border carries a darkened copy of the card's own surface colour;
        // the inner box, inset by 1px at the bottom, carries a lightened copy. What
        // shows through at the bottom is the depth line.
        Background = new SolidColorBrush(Blend(surface, Colors.Black, 0.22));
        _inner.Background = new SolidColorBrush(Blend(surface, Colors.White, 0.14));
        _text.Foreground = textBright;
    }

    /// <summary>Resolves one of the host's <c>Sholto…</c> theme brushes. The second
    /// lookup is the one that works: a resource lookup started at this chip returns
    /// nothing for these keys even when it is attached and painting, while the very
    /// same keys resolve from the window — Sholto declares its palette in
    /// <c>Window.Resources</c> and rewrites it there on every theme change. Without
    /// asking the window directly every chip fell back to the literal below, which is
    /// why chips came out the same navy in all eleven themes despite the class comment
    /// above promising the opposite. Same fix as
    /// <c>FaceplateOverlay.ResourceBrush</c>.</summary>
    private IBrush ResourceBrush(string key, IBrush fallback)
    {
        if (this.TryGetResource(key, ActualThemeVariant, out var value) && value is IBrush brush)
            return brush;
        var top = TopLevel.GetTopLevel(this);
        if (top is not null && top.TryGetResource(key, top.ActualThemeVariant, out var v) && v is IBrush b)
            return b;
        return fallback;
    }

    private Color ResourceColor(string key, Color fallback) =>
        ResourceBrush(key, new SolidColorBrush(fallback)) is SolidColorBrush solid
            ? solid.Color
            : fallback;

    private static Color Blend(Color a, Color b, double t) => Color.FromArgb(
        a.A,
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));
}
