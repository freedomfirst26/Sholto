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

    /// <param name="requiredSteps">Step names whose failure makes
    /// <see cref="HasRequiredFailure"/> true — see that property.</param>
    public AnalysisReport(string filePath, IEnumerable<string> requiredSteps)
    {
        FilePath = filePath;
        _requiredSteps = new HashSet<string>(requiredSteps);
    }

    private readonly Dictionary<string, AnalysisStepStatus> _steps = new();
    // Writes (GetOrCreate) happen on whichever analyser thread is reporting;
    // reads (Overall / IsBusy / AllComplete) usually happen on the UI thread
    // when handling the Updated event. A plain Dictionary throws
    // InvalidOperationException if a read enumerates while a write is in flight.
    private readonly object _stepsGate = new();

    /// <summary>Snapshot of registered steps. Returns a fresh dictionary so
    /// callers can iterate without racing concurrent <see cref="GetOrCreate"/>.</summary>
    public IReadOnlyDictionary<string, AnalysisStepStatus> Steps
    {
        get { lock (_stepsGate) return new Dictionary<string, AnalysisStepStatus>(_steps); }
    }

    public AnalysisStepStatus GetOrCreate(string stepName)
    {
        lock (_stepsGate)
        {
            if (!_steps.TryGetValue(stepName, out var s))
            {
                s = new AnalysisStepStatus(stepName);
                s.PropertyChanged += (_, _) => NotifyAggregate();
                _steps[stepName] = s;
                // Fire outside the lock would be cleaner, but callers don't
                // re-enter GetOrCreate from PropertyChanged handlers, so this is safe.
                NotifyAggregate();
            }
            return s;
        }
    }

    /// <summary>Fraction of total registered steps that have completed (counting Running by its progress).</summary>
    public double Overall
    {
        get
        {
            lock (_stepsGate)
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
    }

    public bool IsBusy
    {
        get { lock (_stepsGate) return _steps.Values.Any(s => s.State == AnalysisState.Running); }
    }

    public bool AllComplete
    {
        get { lock (_stepsGate) return _steps.Values.All(s => s.State == AnalysisState.Complete); }
    }

    /// <summary>True if any step of this track ended in <see cref="AnalysisState.Failed"/>.
    /// Note a failed step makes <see cref="IsBusy"/> go false exactly like a successful
    /// one, so anything watching only IsBusy cannot tell a failure from a success —
    /// which is how five days of broken demucs runs went unnoticed.</summary>
    public bool HasFailure
    {
        get { lock (_stepsGate) return _steps.Values.Any(s => s.State == AnalysisState.Failed); }
    }

    /// <summary>Which step names are required (currently just "beats"/madmom) —
    /// passed in by the caller (see <see cref="AnalysisReport(string, IEnumerable{string})"/>)
    /// rather than hardcoded here, since <c>ExternalTools.IToolDefinition.IsRequired</c>
    /// already owns this fact per tool descriptor.</summary>
    private readonly HashSet<string> _requiredSteps;

    /// <summary>True if a REQUIRED step ended in <see cref="AnalysisState.Failed"/>.
    /// Unlike <see cref="HasFailure"/>, this ignores optional-step failures (stems,
    /// segments) — a track whose demucs run failed but whose beatgrid is fine is
    /// still genuinely analysed, so its row should keep the green tick; a track
    /// whose beatgrid failed to (re)compute must not, even if it happens to have
    /// BPM + stems cached from a previous successful run. See TrackRow.AnalysisState.</summary>
    public bool HasRequiredFailure
    {
        get
        {
            lock (_stepsGate)
                return _steps.Values.Any(s => s.State == AnalysisState.Failed && _requiredSteps.Contains(s.StepName));
        }
    }

    /// <summary>Every failed step as "step: message" lines, or null if nothing failed.
    /// This is what the UI shows in the tooltip behind the failure marker.</summary>
    public string? FailureMessage
    {
        get
        {
            lock (_stepsGate)
            {
                var failed = _steps.Values
                    .Where(s => s.State == AnalysisState.Failed)
                    .Select(s => $"{s.StepName}: {s.Message ?? "failed"}")
                    .ToArray();
                return failed.Length == 0 ? null : string.Join("\n", failed);
            }
        }
    }

    private void NotifyAggregate()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Overall)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AllComplete)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasFailure)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasRequiredFailure)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FailureMessage)));
    }
}

/// <summary>
/// One per app instance. Hands out an <see cref="AnalysisReport"/> per track path
/// so anywhere in the app can read or update analysis progress. The same instance
/// is shared by view models and by the analysis routines that produce the data.
/// </summary>
public sealed class AnalysisReporter : IAnalysisReporter
{
    private readonly Dictionary<string, AnalysisReport> _byPath = new();
    private readonly object _gate = new();
    private readonly IReadOnlyCollection<string> _requiredSteps;

    /// <param name="requiredSteps">Step names a track cannot be considered analysed
    /// without (e.g. "beats"/madmom) — handed to every <see cref="AnalysisReport"/>
    /// this reporter creates. Passed in explicitly rather than hardcoded, since
    /// <c>ExternalTools.IToolDefinition.IsRequired</c> per tool descriptor is the
    /// single source of truth for which steps those are.</param>
    public AnalysisReporter(IReadOnlyCollection<string> requiredSteps)
    {
        _requiredSteps = requiredSteps;
    }

    /// <summary>Raised on the analyser thread whenever a report's status flips or progresses.</summary>
    public event Action<AnalysisReport>? Updated;

    public AnalysisReport ReportFor(string filePath)
    {
        lock (_gate)
        {
            if (!_byPath.TryGetValue(filePath, out var r))
            {
                r = new AnalysisReport(filePath, _requiredSteps);
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
