using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Views;

public partial class DeckView : UserControl
{
    public DeckView()
    {
        InitializeComponent();

        // Forward grid-edit waveform clicks to the deck VM (two-point grid).
        var wave = this.FindControl<Controls.WaveformControl>("Waveform");
        if (wave is not null)
            wave.GridAnchorClicked += secs =>
            {
                if (DataContext is DeckViewModel vm) vm.ClickGrid(secs);
            };
    }

    /// <summary>Click the BPM to open the combined tune editor that slides out over
    /// the disc (↑↓ BPM, ←→ grid; ½ and reset inline).</summary>
    private void OnBpmPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is DeckViewModel vm) vm.ToggleEditor();
    }

    /// <summary>All tune-editor buttons route here; the button's Tag names the action.
    /// One handler keeps the XAML declarative and the wiring in one place.</summary>
    private void OnTuneAction(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not DeckViewModel vm) return;
        if (sender is not Control c || c.Tag is not string action) return;
        switch (action)
        {
            case "half":   vm.ChangeBpm(BpmMultiplierOp.Toggle); break;
            case "bpm+":   vm.AdjustBpm(+0.1);                   break;
            case "bpm-":   vm.AdjustBpm(-0.1);                   break;
            case "phase-": vm.NudgeGridFine(-0.010);             break;
            case "phase+": vm.NudgeGridFine(+0.010);             break;
            case "reset":  vm.ResetToAnalysis();                 break;
            case "close":  vm.CloseEditor();                     break;
        }
    }

    /// <summary>Click anywhere outside the editor (the transparent backdrop) closes it.</summary>
    private void OnEditBackdropPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is DeckViewModel vm) vm.CloseEditor();
    }
}
