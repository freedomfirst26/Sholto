namespace Sholto.App.ExternalTools;

/// <summary>One tool's boot-time resolution result: whether it was found and where,
/// alongside the capability it backs and whether the app can run without it.</summary>
public sealed record ToolPresence(string ToolName, string Capability, bool Required, string? BinaryPath)
{
    public bool IsPresent => BinaryPath is not null;
}
