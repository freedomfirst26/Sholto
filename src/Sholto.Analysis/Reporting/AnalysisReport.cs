using System.ComponentModel;
using Sholto.Analysis.Data;
using Sholto.Analysis.Processing;

namespace Sholto.Analysis.Reporting;

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
