using Sholto.Data;

namespace Sholto.App.ExternalTools;

using System.Linq;

/// <summary>
/// Which arm's-length tools this build knows about: (binary name, capability label,
/// required) for each. The single source both <see cref="ToolSet"/>'s resolution and
/// <see cref="SystemCheck"/>'s reporting derive from, so the two can't drift apart the
/// way a second, hand-maintained list would. Adding a fifth tool is one const in
/// <see cref="ExternalToolNames"/> plus one entry here.
///
/// An instance, built by <see cref="ExternalToolOptionsFactory"/> and handed to
/// <see cref="ExternalToolOptions.Tools"/> and <see cref="ToolSet"/>.
/// </summary>
public sealed class ExternalToolCatalog : IExternalToolCatalog
{
    /// <summary>(binary name, capability label, required) for every tool this layer resolves.</summary>
    public IReadOnlyList<(string Name, string Capability, bool Required)> Descriptors { get; } =
    [
        (ExternalToolNames.Madmom, ToolCapabilities.Beats, true),
        (ExternalToolNames.Demucs, ToolCapabilities.Stems, false),
        (ExternalToolNames.Ffmpeg, ToolCapabilities.Transcode, false),
    ];

    /// <summary>Every tool name, derived from <see cref="Descriptors"/>.</summary>
    public IReadOnlyList<string> All { get; }

    public ExternalToolCatalog()
    {
        All = Descriptors.Select(d => d.Name).ToArray();
    }
}
