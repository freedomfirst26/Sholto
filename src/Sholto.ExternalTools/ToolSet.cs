using Sholto.Analysis;

namespace Sholto.ExternalTools;

/// <summary>
/// Composition-root helper: resolves every tool named in
/// <see cref="ExternalToolOptions.Tools"/> to a binary path (or null) ONCE, and hands
/// out a pre-bound <see cref="IExternalTool"/> per tool via <see cref="For"/>.
///
/// Deliberately knows nothing about per-tool cache/output layout — that used to be
/// threaded through here as a <c>(toolName, inputPath) -&gt; workspace directory</c>
/// function so <see cref="IExternalTool.RunAsync{T}"/> could compute its own work
/// directory, but <c>Sholto.ExternalTools</c> has zero project references, so it
/// could never really own that policy (it doesn't know about the SHA256-based
/// directory encoding <c>ExternalToolStack.FromEnvironment</c> uses). Callers that need a deterministic work
/// directory (the on-disk stem cache) now own that policy directly and pass it to
/// <see cref="IExternalTool.RunAsync{T}"/> themselves — see
/// <c>DemucsStemAnalysisStep</c>/<c>DemucsStemPresence</c> in <c>Sholto.Analysis</c>.
/// </summary>
public sealed class ToolSet
{
    private readonly IExternalToolRunner _runner;
    private readonly IReadOnlyDictionary<string, string?> _resolvedPaths;
    private readonly string _executableSuffix;

    public ToolSet(ExternalToolOptions options, IExternalToolRunner runner)
    {
        _runner = runner;
        _executableSuffix = options.ExecutableSuffix;

        // The only place an ExternalToolFinder is ever constructed — resolving
        // availability is this type's job, not the composition root's; App.axaml.cs
        // used to build a second finder of its own just to resolve ffmpeg's path
        // (see PathOrName below), which meant "resolve once" wasn't actually true.
        var finder = new ExternalToolFinder(options);
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
    /// <see cref="ExternalToolFinder.LocateOrName"/> without constructing a second
    /// finder to get it.</summary>
    public string PathOrName(string toolName) =>
        _resolvedPaths.GetValueOrDefault(toolName) ?? (toolName + _executableSuffix);
}
