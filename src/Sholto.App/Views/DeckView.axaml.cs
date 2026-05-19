using System;
using Avalonia.Controls;
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
}
