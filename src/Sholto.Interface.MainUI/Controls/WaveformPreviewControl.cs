using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Controls;

/// <summary>A Layout Wizard preview card's waveform: a pre-baked image of the whole demo track, one column
/// per pixel, scrolled left under a fixed playhead and looping. A frame only moves the source rectangle:
/// no bake, no per-frame brushes. The faded, already-played part sits left of the playhead like the decks'.</summary>
public sealed class WaveformPreviewControl : Control
{
    private const double PlayheadFraction = 0.45;
    private const double PlayheadWidth = 2;

    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<WaveformPreviewControl, Bitmap?>(nameof(Source));

    public static readonly StyledProperty<IWaveformPreviewScroll?> ScrollProperty =
        AvaloniaProperty.Register<WaveformPreviewControl, IWaveformPreviewScroll?>(nameof(Scroll));

    /// <summary>The tried-on theme's colours (bound via DynamicResource SholtoWaveformPalette). The played-part
    /// fade comes from its Background and the playhead from its Playhead; null draws neither.</summary>
    public static readonly StyledProperty<WaveformPalette?> PaletteProperty =
        AvaloniaProperty.Register<WaveformPreviewControl, WaveformPalette?>(nameof(Palette));

    private IBrush? _playedFade;
    private IBrush? _playhead;

    public WaveformPalette? Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public IWaveformPreviewScroll? Scroll
    {
        get => GetValue(ScrollProperty);
        set => SetValue(ScrollProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScrollProperty)
        {
            if (change.OldValue is IWaveformPreviewScroll old) old.Moved -= OnMoved;
            if (change.NewValue is IWaveformPreviewScroll next) next.Moved += OnMoved;
            InvalidateVisual();
        }
        else if (change.Property == SourceProperty)
        {
            InvalidateVisual();
        }
        else if (change.Property == PaletteProperty)
        {
            RebuildBrushes(change.NewValue as WaveformPalette);
            InvalidateVisual();
        }
    }

    private void RebuildBrushes(WaveformPalette? palette)
    {
        if (palette is null)
        {
            _playedFade = null;
            _playhead = null;
            return;
        }
        Color back = palette.Background;
        _playedFade = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.FromArgb(0xD0, back.R, back.G, back.B), 0),
                new GradientStop(Color.FromArgb(0x1A, back.R, back.G, back.B), 1),
            ],
        }.ToImmutable();
        _playhead = new SolidColorBrush(palette.Playhead).ToImmutable();
    }

    private void OnMoved(object? sender, EventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        if (Source is not { } image) return;
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        double imageWidth = image.PixelSize.Width;
        double imageHeight = image.PixelSize.Height;
        double playheadX = w * PlayheadFraction;
        double position = Scroll?.Position ?? 0;
        double left = (position - playheadX) % imageWidth;
        if (left < 0) left += imageWidth;

        // The track may end inside the card: carry on from its start so the loop has no gap.
        double x = 0;
        while (x < w)
        {
            double take = Math.Min(w - x, imageWidth - left);
            context.DrawImage(image, new Rect(left, 0, take, imageHeight), new Rect(x, 0, take, h));
            x += take;
            left = 0;
        }

        if (_playedFade is not null)
            context.DrawRectangle(_playedFade, null, new Rect(0, 0, playheadX, h));
        if (_playhead is not null)
            context.DrawRectangle(_playhead, null, new Rect(playheadX - PlayheadWidth / 2, 0, PlayheadWidth, h));
    }
}
