namespace Sholto.TestSupport.Allocation;

public sealed record AllocationWindowsResult(IReadOnlyList<long> Bytes)
{
    public long Smallest => Bytes.Min();

    public int AllocatingWindows => Bytes.Count(b => b != 0);

    public override string ToString() => "bytes allocated per window: " + string.Join(", ", Bytes);
}
