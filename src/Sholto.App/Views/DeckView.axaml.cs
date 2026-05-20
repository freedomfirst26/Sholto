using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Sholto.App.ViewModels;

namespace Sholto.App.Views;

public partial class DeckView : UserControl
{
    private readonly DispatcherTimer _flashTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(400),
    };
    private double _flashPhase;

    public DeckView()
    {
        InitializeComponent();
        _flashTimer.Tick += OnFlashTick;
        _flashTimer.Start();
    }

    private void OnFlashTick(object? sender, EventArgs e)
    {
        if (DataContext is not DeckViewModel vm) return;
        var ring = this.FindControl<Border>("DiscRing");
        if (ring is null) return;

        if (vm.IsNearEnd)
        {
            _flashPhase = _flashPhase < 0.5 ? 1.0 : 0.35;
            ring.Opacity = _flashPhase;
        }
        else if (ring.Opacity != 1.0)
        {
            ring.Opacity = 1.0;
        }
    }

    /// <summary>Click the BPM to flip-flop between the analyser's value and the
    /// corrected half/double. One click toggles, another returns to original.
    /// The chip jumps up + flips card-style; the BPM swaps at the apex (when the
    /// chip is squished to a thin line) so the new number is revealed as it lands.</summary>
    private async void OnBpmPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is not DeckViewModel vm) return;

        if (sender is not Border chip)
        {
            vm.ToggleBpmOverride();
            return;
        }

        await FlipBpmChipAsync(chip, vm.ToggleBpmOverride);
    }

    private static async Task FlipBpmChipAsync(Border chip, Action swapAtApex)
    {
        // The chip already has a TransformOperationsTransition on RenderTransform
        // (see bpm-chip-main style), so setting the transform directly drives the
        // existing smoothed motion — no separate keyframe Animation needed.
        // Phase 1: rise + squish to a sliver (digits invisible).
        // Phase 2 (after swap): drop + expand, revealing the new number.
        var lift = TransformOperations.Parse("translateY(-22px) scaleY(0.05)");
        var land = TransformOperations.Parse("translateY(0px) scaleY(1)");

        chip.RenderTransform = lift;
        await Task.Delay(160);
        swapAtApex();
        chip.RenderTransform = land;
    }
}
