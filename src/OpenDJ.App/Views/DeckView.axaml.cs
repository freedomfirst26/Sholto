using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OpenDJ.App.ViewModels;

namespace OpenDJ.App.Views;

public partial class DeckView : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<DeckView, string>(nameof(Label), "DECK");

    /// <summary>Header text e.g. "DECK A", "DECK B".</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

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

    private void OnEject(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DeckViewModel vm) vm.Unload();
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
