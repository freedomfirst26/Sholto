namespace Sholto.App.ExternalTools;

/// <summary>Locates an external tool's binary on disk. Built once at bootstrap and
/// handed to <see cref="ToolSet"/>.</summary>
public interface IExternalToolFinder
{
    /// <summary>The path of <paramref name="name"/>, or null if it isn't installed
    /// anywhere we look.</summary>
    string? Locate(string name);
}
