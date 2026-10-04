namespace Sholto.App.Performance;

/// <summary>The performance buckets, built together over one jog-recency instance.</summary>
public sealed class PerformanceStack(
    IPlatter platter, IScratchEngine scratch, IMagnetSnap magnet, IPerformanceTick tick)
{
    public IPlatter Platter { get; } = platter;
    public IScratchEngine Scratch { get; } = scratch;
    public IMagnetSnap Magnet { get; } = magnet;
    /// <summary>The frame run; call <see cref="IPerformanceTick.Start"/> once the app is wired.</summary>
    public IPerformanceTick Tick { get; } = tick;
}
