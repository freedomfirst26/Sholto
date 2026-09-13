using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Analysis.Data;

namespace Sholto.Analysis.Processing;

/// <summary>
/// Status of a single analysis step (e.g. "waveform", "beats", "key", "stems")
/// for a single track. Raises PropertyChanged so UIs can bind to it directly.
/// </summary>
public sealed class AnalysisStepStatus : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public string StepName { get; }
    public AnalysisStepStatus(string stepName) { StepName = stepName; }

    private AnalysisState _state = AnalysisState.NotStarted;
    public AnalysisState State
    {
        get => _state;
        set { if (_state == value) return; _state = value; Notify(); }
    }

    private double _progress;
    /// <summary>0..1, only meaningful when <see cref="State"/> == Running.</summary>
    public double Progress
    {
        get => _progress;
        set { if (Math.Abs(_progress - value) < 0.001) return; _progress = value; Notify(); }
    }

    private string? _message;
    public string? Message
    {
        get => _message;
        set { if (_message == value) return; _message = value; Notify(); }
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
