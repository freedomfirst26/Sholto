namespace Sholto.TestSupport.Allocation;

/// <summary>
/// Runs a hot-path window several times and records the bytes the current thread allocated in each.
/// No warm-up of its own: the caller keeps its existing warm-up.
/// </summary>
public sealed class AllocationWindows(int count)
{
    private readonly int _count = count;

    public AllocationWindowsResult Measure(Action window)
    {
        var bytes = new long[_count];
        for (var i = 0; i < bytes.Length; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            window();
            bytes[i] = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        return new AllocationWindowsResult(bytes);
    }
}
