using Sholto.Data;

namespace Sholto.App.Glance;

/// <summary>The most recent accepted loads of this run, newest first, deduplicated and capped. Announced as
/// <see cref="RecentLoadsChanged"/>. Not persisted. App thread only.</summary>
public interface IRecentLoads
{
    /// <summary>The most loads kept.</summary>
    int Capacity { get; }

    /// <summary>File paths, newest first.</summary>
    IReadOnlyList<string> Paths { get; }
}
