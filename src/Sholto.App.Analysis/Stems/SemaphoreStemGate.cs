namespace Sholto.App.Analysis.Stems;

/// <summary><see cref="IStemGate"/> backed by a single-slot semaphore. One instance is shared by every deck.</summary>
public sealed class SemaphoreStemGate : IStemGate
{
    private readonly SemaphoreSlim _slot = new(1, 1);

    public async Task<IDisposable> EnterAsync(CancellationToken ct = default)
    {
        await _slot.WaitAsync(ct);
        return new Release(_slot);
    }

    private sealed class Release(SemaphoreSlim slot) : IDisposable
    {
        private readonly SemaphoreSlim _slot = slot;
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) _slot.Release();
        }
    }
}
