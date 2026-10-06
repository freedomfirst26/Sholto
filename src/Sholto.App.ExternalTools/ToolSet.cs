using System.Linq;
using Sholto.App.Analysis;
using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.ExternalTools;

/// <summary>
/// Composition-root helper: resolves every tool named in
/// <see cref="ExternalToolOptions.Tools"/> to a binary path (or null) ONCE, and hands
/// out a pre-bound <see cref="IExternalTool"/> per tool via <see cref="For"/>.
///
/// Deliberately knows nothing about per-tool cache/output layout — that used to be
/// threaded through here as a <c>(toolName, inputPath) -&gt; workspace directory</c>
/// function so <see cref="IExternalTool.RunAsync{T}"/> could compute its own work
/// directory, but <c>Sholto.App.ExternalTools</c> references only <c>Sholto.App.Analysis</c>,
/// so it could never really own that policy (it doesn't know about the SHA256-based
/// directory encoding <c>ExternalToolStackFactory</c> uses; the options themselves come from
/// <c>ExternalToolOptionsFactory.FromEnvironment</c>). Callers that need a deterministic work
/// directory (the on-disk stem cache) now own that policy directly and pass it to
/// <see cref="IExternalTool.RunAsync{T}"/> themselves — see
/// <c>DemucsStemAnalysisStep</c> in <c>Sholto.App.ExternalTools</c> and
/// <c>DemucsStemPresence</c> in <c>Sholto.App.Analysis.Stems</c>.
/// </summary>
public sealed class ToolSet
{
    private readonly IExternalToolRunner _runner;
    private readonly IReadOnlyDictionary<string, string?> _resolvedPaths;
    private readonly string _executableSuffix;
    private readonly IExternalToolCatalog _catalog;

    public ToolSet(ExternalToolOptions options, IExternalToolRunner runner, IExternalToolCatalog catalog, IExternalToolFinder finder)
    {
        _runner = runner;
        _catalog = catalog;
        _executableSuffix = options.ExecutableSuffix;

        // The finder is handed in by the composition root (ExternalToolStackFactory);
        // resolving availability happens here, once, so "resolve once" stays true.
        var resolved = new Dictionary<string, string?>(options.Tools.Count);
        foreach (var name in options.Tools)
            resolved[name] = finder.Locate(name);
        _resolvedPaths = resolved;
    }

    /// <summary>The pre-bound configured tool for <paramref name="toolName"/> (one of
    /// <see cref="ExternalToolNames"/>). Unresolvable/unknown names come back with a
    /// null <see cref="IExternalTool.BinaryPath"/> rather than throwing — same
    /// "absence degrades gracefully" contract the rest of this layer follows.</summary>
    public IExternalTool For(string toolName) =>
        new ConfiguredTool(_runner, _resolvedPaths.GetValueOrDefault(toolName));

    /// <summary>The resolved path for <paramref name="toolName"/>, or its bare
    /// (suffixed) name so the OS resolves it via <c>PATH</c> itself — for the one
    /// tool consumed as a plain string rather than through <see cref="For"/>:
    /// ffmpeg, which <c>FfmpegDecodeStrategy</c> takes directly because it's a
    /// streaming decoder, not run through <see cref="IExternalToolRunner"/>. Mirrors
    /// <c>ExternalToolFinder.LocateOrName</c> without needing a second
    /// finder to get it.</summary>
    public string PathOrName(string toolName) =>
        _resolvedPaths.GetValueOrDefault(toolName) ?? (toolName + _executableSuffix);

    /// <summary>Names the boot-time probe this constructor already ran and reports
    /// its result — built purely by reading <see cref="_resolvedPaths"/>, no finder,
    /// no filesystem access, no re-resolving.</summary>
    public SystemCheck Check() =>
        new(_catalog.Descriptors
            .Select(d => new ToolPresence(d.Name, d.Capability, d.Required, _resolvedPaths.GetValueOrDefault(d.Name)))
            .ToList());
}
