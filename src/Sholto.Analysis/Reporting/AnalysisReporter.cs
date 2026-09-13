using System.ComponentModel;
using Sholto.Analysis.Data;

namespace Sholto.Analysis.Reporting;

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
