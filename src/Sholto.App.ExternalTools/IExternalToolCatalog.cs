namespace Sholto.App.ExternalTools;

/// <summary>Which arm's-length tools this build knows about.</summary>
public interface IExternalToolCatalog
{
    /// <summary>(binary name, capability label, required) for every tool this layer resolves.</summary>
    IReadOnlyList<(string Name, string Capability, bool Required)> Descriptors { get; }

    /// <summary>Every tool name, derived from <see cref="Descriptors"/>.</summary>
    IReadOnlyList<string> All { get; }
}
