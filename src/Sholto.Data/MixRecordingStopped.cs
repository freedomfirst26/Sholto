namespace Sholto.Data;

/// <summary>A recording attempt ended. Fact: published exactly once per attempt, not replayed.</summary>
/// <param name="FilePath">The file written; null when the recording never started (the path or the open failed).</param>
/// <param name="Reason">Why it ended early; null for a normal stop.</param>
public readonly record struct MixRecordingStopped(string? FilePath, string? Reason) : IEvent;
