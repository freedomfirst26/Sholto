namespace Sholto.App.Analysis.Stems;

/// <summary>Admits one demucs run at a time across every deck. Two concurrent GPU runs would
/// compete for VRAM; two CPU runs would compete for every core.</summary>
public interface IStemGate
{
    /// <summary>Wait for the gate. Dispose the result to let the next run in.</summary>
    Task<IDisposable> EnterAsync(CancellationToken ct = default);
}
