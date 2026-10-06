namespace Sholto.Data;

/// <summary>One external tool's boot-time resolution result, as told to the interfaces.</summary>
/// <param name="ToolName">The binary's name.</param>
/// <param name="Capability">What the tool backs; one of the <see cref="ToolCapabilities"/> constants.</param>
/// <param name="Required">Whether the app can run without it.</param>
/// <param name="BinaryPath">Where it was found, or null when it was not.</param>
/// <param name="InstallCommand">The command that installs it, when the app knows one; otherwise null.</param>
public sealed record ToolStatus(string ToolName, string Capability, bool Required, string? BinaryPath, string? InstallCommand)
{
    public bool IsPresent => BinaryPath is not null;
}
