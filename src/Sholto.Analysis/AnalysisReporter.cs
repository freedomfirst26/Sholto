using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sholto.Analysis;

/// <summary>Lifecycle state of one analysis step on one track.</summary>
public enum AnalysisState
{
    NotStarted,
    Running,
    Complete,
    Failed,
}

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

/// <summary>
/// All analysis steps for one track, plus an aggregate progress (0..1) over all known steps.
/// </summary>
public sealed class AnalysisReport : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public string FilePath { get; }
    public AnalysisReport(string filePath) { FilePath = filePath; }

    private readonly Dictionary<string, AnalysisStepStatus> _steps = new();
    public IReadOnlyDictionary<string, AnalysisStepStatus> Steps => _steps;

    public AnalysisStepStatus GetOrCreate(string stepName)
    {
        if (!_steps.TryGetValue(stepName, out var s))
        {
            s = new AnalysisStepStatus(stepName);
            s.PropertyChanged += (_, _) => NotifyAggregate();
            _steps[stepName] = s;
            NotifyAggregate();
        }
        return s;
    }

    /// <summary>Fraction of total registered steps that have completed (counting Running by its progress).</summary>
    public double Overall
    {
        get
        {
            if (_steps.Count == 0) return 0;
            double sum = 0;
            foreach (var s in _steps.Values)
            {
                sum += s.State switch
                {
                    AnalysisState.Complete => 1.0,
                    AnalysisState.Running  => Math.Clamp(s.Progress, 0, 1),
                    _ => 0.0,
                };
            }
            return sum / _steps.Count;
        }
    }

    public bool IsBusy => _steps.Values.Any(s => s.State == AnalysisState.Running);
    public bool AllComplete => _steps.Values.All(s => s.State == AnalysisState.Complete);

    private void NotifyAggregate()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Overall)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AllComplete)));
    }
}

/// <summary>
/// One per app instance. Hands out an <see cref="AnalysisReport"/> per track path
/// so anywhere in the app can read or update analysis progress. The same instance
/// is shared by view models and by the analysis routines that produce the data.
/// </summary>
public sealed class AnalysisReporter
{
    private readonly Dictionary<string, AnalysisReport> _byPath = new();
    private readonly object _gate = new();

    /// <summary>Raised on the analyser thread whenever a report's status flips or progresses.</summary>
    public event Action<AnalysisReport>? Updated;

    public AnalysisReport ReportFor(string filePath)
    {
        lock (_gate)
        {
            if (!_byPath.TryGetValue(filePath, out var r))
            {
                r = new AnalysisReport(filePath);
                r.PropertyChanged += (_, _) => Updated?.Invoke(r);
                _byPath[filePath] = r;
            }
            return r;
        }
    }

    /// <summary>Convenience: mark <paramref name="stepName"/> as Running with given progress.</summary>
    public void Running(string filePath, string stepName, double progress = 0, string? message = null)
    {
        var step = ReportFor(filePath).GetOrCreate(stepName);
        step.State = AnalysisState.Running;
        step.Progress = progress;
        step.Message = message;
    }

    public void Complete(string filePath, string stepName, string? message = null)
    {
        var step = ReportFor(filePath).GetOrCreate(stepName);
        step.State = AnalysisState.Complete;
        step.Progress = 1.0;
        step.Message = message;
    }

    public void Failed(string filePath, string stepName, string message)
    {
        var step = ReportFor(filePath).GetOrCreate(stepName);
        step.State = AnalysisState.Failed;
        step.Message = message;
    }
}
