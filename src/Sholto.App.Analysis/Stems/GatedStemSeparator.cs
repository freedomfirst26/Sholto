using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;

namespace Sholto.App.Analysis.Stems;

/// <summary>See <see cref="IStemSeparator"/>. Runs <paramref name="step"/> (the caching step: a hit is instant)
/// inside <paramref name="gate"/>, the same single slot <c>StemAnalysisStage</c> uses, so a re-analysis never
/// runs demucs alongside a deck's run. A re-analysis queued behind a run of the same track finds its stems cached
/// once the gate opens.</summary>
public sealed class GatedStemSeparator(IStemAnalysisStep step, IStemGate gate, IAnalysisReporter reporter) : IStemSeparator
{
    private readonly IStemAnalysisStep _step = step;
    private readonly IStemGate _gate = gate;
    private readonly IAnalysisReporter _reporter = reporter;

    public bool IsAvailable => _step.IsAvailable;

    public async Task<StemPaths> SeparateAsync(string filePath, CancellationToken ct = default)
    {
        // Busy on the row from the moment it queues, not only once demucs starts.
        _reporter.Running(filePath, AnalysisSteps.Stems, 0, "waiting for stems");
        try
        {
            using (await _gate.EnterAsync(ct))
            {
                // A free semaphore admits a caller whose token is already cancelled; a superseded run must not start demucs.
                ct.ThrowIfCancellationRequested();
                return await _step.AnalyzeAsync(filePath, _reporter, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _reporter.Cancelled(filePath, AnalysisSteps.Stems);
            throw;
        }
        catch (Exception ex)
        {
            _reporter.Failed(filePath, AnalysisSteps.Stems, ex.Message);
            throw;
        }
    }
}
