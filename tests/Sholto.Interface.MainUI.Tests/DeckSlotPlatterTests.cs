using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sholto.Interface.MainUI.Controls;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The platter keeps its pens, ring geometry and number text between frames and rebuilds them only
/// when a brush, the number or a ring degree changes.</summary>
public class DeckSlotPlatterTests
{
    private sealed class StubSlot : IDeckSlotViewModel
    {
        public int Number { get; set; } = 1;
        public bool IsLoaded { get; set; } = true;
        public bool IsPlaying { get; set; } = true;
        public bool IsTarget { get; set; }
        public bool IsReference => false;
        public bool IsCaution => false;
        public bool IsArmed => false;
        public string Title => "";
        public string Camelot => "";
        public IBrush? KeyBrush => null;
        public string BpmText => "";
        public bool IsLow { get; set; }
        public bool ShowsKeyCap => false;
        public string KeyCapLabel => "";
        public string CautionText => "";
        public string ArmedText => "";
        public string EmptyTitle => "";
        public string EmptyHint => "";
        public string KeyCapText => "";
        public bool IsSweeping => false;
        public bool ShowStats => false;
        public bool ShowCaution => false;
        public bool ShowArmed => false;
        public double RemainingFraction { get; set; } = 0.5;
        public double SpinTurns { get; set; }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public event Action? Changed;
        public void Raise() => Changed?.Invoke();
    }

    public DeckSlotPlatterTests() => AvaloniaTestApp.EnsureStarted();

    private static DeckSlotPlatter Control(StubSlot slot) => new()
    {
        Width = 30,
        Height = 30,
        Slot = slot,
        TrackBrush = new SolidColorBrush(Colors.DimGray),
        PlayingBrush = new SolidColorBrush(Colors.LimeGreen),
        LowBrush = new SolidColorBrush(Colors.Red),
        MutedBrush = new SolidColorBrush(Colors.Gray),
        AccentBrush = new SolidColorBrush(Colors.Gold),
        DiscBrush = new SolidColorBrush(Colors.Black),
        RaisedBrush = new SolidColorBrush(Colors.DarkGray),
        LabelBrush = new SolidColorBrush(Colors.DarkSlateGray),
        MarkerBrush = new SolidColorBrush(Colors.White),
    };

    private static void Draw(DeckSlotPlatter c, RenderTargetBitmap rtb, int frames, Action<int> perFrame)
    {
        using var ctx = rtb.CreateDrawingContext();
        for (var i = 0; i < frames; i++)
        {
            perFrame(i);
            c.Render(ctx);
        }
    }

    [Fact]
    public void Turning_the_platter_rebuilds_nothing()
    {
        var slot = new StubSlot();
        var c = Control(slot);
        c.Measure(new Size(30, 30));
        c.Arrange(new Rect(0, 0, 30, 30));
        using var rtb = new RenderTargetBitmap(new PixelSize(30, 30));
        Draw(c, rtb, 3, _ => { });
        var built = c.PaintBuilds;

        Draw(c, rtb, 500, i => slot.SpinTurns = (i % 360) / 360.0);

        Assert.Equal(built, c.PaintBuilds);
    }

    [Fact]
    public void A_new_ring_degree_rebuilds_only_the_arc()
    {
        var slot = new StubSlot();
        var c = Control(slot);
        using var rtb = new RenderTargetBitmap(new PixelSize(30, 30));
        Draw(c, rtb, 3, _ => { });
        var built = c.PaintBuilds;

        Draw(c, rtb, 1, _ => slot.RemainingFraction = 0.25);
        Assert.Equal(built + 1, c.PaintBuilds);

        Draw(c, rtb, 10, _ => slot.RemainingFraction = 0.25);
        Assert.Equal(built + 1, c.PaintBuilds);
    }

    [Fact]
    public void A_new_brush_rebuilds_its_pens_and_an_unchanged_one_does_not()
    {
        var c = Control(new StubSlot());
        using var rtb = new RenderTargetBitmap(new PixelSize(30, 30));
        Draw(c, rtb, 2, _ => { });
        var built = c.PaintBuilds;

        c.PlayingBrush = new SolidColorBrush(Colors.Green);
        Draw(c, rtb, 5, _ => { });

        Assert.Equal(built + 1, c.PaintBuilds);
    }

    [Fact]
    public void A_ghost_record_measures_64_and_draws_its_disc_only_while_the_deck_is_empty()
    {
        var slot = new StubSlot { IsLoaded = false, IsPlaying = false, IsTarget = false };
        var c = Control(slot);
        c.Ghost = true;
        c.Width = double.NaN;
        c.Height = double.NaN;
        c.Measure(new Size(200, 200));
        Assert.Equal(new Size(64, 64), c.DesiredSize);
        using var rtb = new RenderTargetBitmap(new PixelSize(64, 64));
        Draw(c, rtb, 1, _ => { });
        Assert.True(c.PaintBuilds > 0);

        var loadedGhost = Control(new StubSlot { IsLoaded = true });
        loadedGhost.Ghost = true;
        Draw(loadedGhost, rtb, 3, _ => { });
        Assert.Equal(0, loadedGhost.PaintBuilds);
    }

    [Fact]
    public void A_steady_ghost_frame_rebuilds_nothing_and_allocates_nothing_on_the_control()
    {
        var slot = new StubSlot { IsLoaded = false, IsPlaying = false, IsTarget = true };
        var c = Control(slot);
        c.Ghost = true;
        using var rtb = new RenderTargetBitmap(new PixelSize(64, 64));
        using var ctx = rtb.CreateDrawingContext();
        for (var i = 0; i < 300; i++) c.Render(ctx);
        var built = c.PaintBuilds;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) c.Render(ctx);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // The same calls, with the same radial gradient, straight on the drawing context: what Avalonia itself records per frame.
        var brush = new SolidColorBrush(Colors.Gray);
        var disc = new RadialGradientBrush { Radius = 0.6, GradientStops = [new GradientStop(Colors.Gray, 0), new GradientStop(Colors.Black, 1)] };
        var pen = new Pen(brush, 1);
        var centre = new Point(15, 15);
        var arc = new StreamGeometry();
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            using (ctx.PushTransform(Matrix.CreateScale(1.7, 1.7)))
            {
                ctx.DrawEllipse(disc, pen, centre, 14, 14);
                for (var g = 0; g < 4; g++) ctx.DrawEllipse(null, pen, centre, 8, 8);
                using (ctx.PushOpacity(0.45)) ctx.DrawGeometry(null, pen, arc);
                ctx.DrawEllipse(brush, null, centre, 5.6, 5.6);
            }
        }
        var floor = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(built, c.PaintBuilds);
        Assert.True(allocated <= floor + 1000 * 16L, $"allocated {allocated} bytes over 1000 frames; the context alone {floor}");

        c.DiscBrush = new SolidColorBrush(Colors.Navy);
        c.Render(ctx);
        Assert.True(c.PaintBuilds > built);
    }

    [Fact]
    public void An_empty_deck_draws_no_loaded_platter()
    {
        var slot = new StubSlot { IsLoaded = false, IsPlaying = false, IsTarget = true };
        var c = Control(slot);
        using var rtb = new RenderTargetBitmap(new PixelSize(30, 30));

        Draw(c, rtb, 3, _ => { });
        var built = c.PaintBuilds;
        Draw(c, rtb, 50, _ => { });

        Assert.Equal(built, c.PaintBuilds);
    }

    [Fact]
    public void A_steady_spinning_frame_draws_without_allocating()
    {
        var slot = new StubSlot();
        var c = Control(slot);
        using var rtb = new RenderTargetBitmap(new PixelSize(30, 30));
        using var ctx = rtb.CreateDrawingContext();
        for (var i = 0; i < 300; i++) { slot.SpinTurns = (i % 360) / 360.0; c.Render(ctx); }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 300; i < 1300; i++) { slot.SpinTurns = (i % 360) / 360.0; c.Render(ctx); }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // The control allocates nothing per frame; the drawing context may record a small fixed amount per
        // call, so bound it per frame rather than demand zero from Avalonia.
        Assert.True(allocated <= 1000 * 256L, $"allocated {allocated} bytes over 1000 frames");
    }

    private static IBrush? RingBrushAfterDraw(DeckSlotPlatter c, int size)
    {
        c.Measure(new Size(size, size));
        c.Arrange(new Rect(0, 0, size, size));
        using var rtb = new RenderTargetBitmap(new PixelSize(size, size));
        using (var ctx = rtb.CreateDrawingContext()) c.Render(ctx);
        return c.RingBrush;
    }

    [Fact]
    public void A_low_slot_draws_its_arc_with_the_low_brush()
    {
        var slot = new StubSlot { IsLow = true, RemainingFraction = 0.5 };
        var c = Control(slot);
        c.Width = 52;
        c.Height = 52;

        Assert.Same(c.LowBrush, RingBrushAfterDraw(c, 52));

        slot.IsLow = false;
        Assert.Same(c.PlayingBrush, RingBrushAfterDraw(c, 52));
    }

    [Fact]
    public void The_platter_measures_to_its_set_size()
    {
        var c = Control(new StubSlot());
        c.Width = 52;
        c.Height = 52;
        c.Measure(new Size(200, 200));

        Assert.Equal(new Size(52, 52), c.DesiredSize);
    }
}
