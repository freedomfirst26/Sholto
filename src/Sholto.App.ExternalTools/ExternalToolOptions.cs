namespace Sholto.App.ExternalTools;

/// <summary>
/// Everything about the current platform that <see cref="ExternalToolFinder"/> and
/// <see cref="ToolSet"/> need, built once at bootstrap and handed down — a
/// plain POCO — it carries settings and decides nothing;
/// <see cref="ExternalToolOptionsFactory"/> builds it. No package reference either, so <c>Sholto.App.ExternalTools</c> keeps its zero-package-reference rule (no
/// <c>Microsoft.Extensions.Options</c> here; <c>Sholto.Interface.MainUI</c> wraps this the same way
/// it wraps the controller mapping options (<c>DdjFlx4Options</c> in <c>Sholto.Interface.Controller.Mappings.DdjFlx4</c>), and unwraps before anything downstream sees it).
///
/// Carries only machine facts — the fixed search directories, the executable suffix
/// (<c>""</c> on Linux, <c>".exe"</c> on Windows), where the on-disk tool caches live
/// — plus which tools exist (<see cref="Tools"/>, built from every name in
/// <see cref="ExternalToolCatalog.All"/>). It deliberately does NOT carry per-tool
/// policy: no "required" flag (that's <c>IToolDefinition.IsRequired</c> on each
/// descriptor — recreating it here would restore the exact duplication that was just
/// removed from <c>AnalysisReporter</c>), and no demucs model directory name (that's a
/// fact about demucs's own output layout, not about this machine — see
/// <c>DemucsTool.ModelDir</c>).
/// </summary>
public sealed class ExternalToolOptions
{
    /// <summary>Fixed directories searched, in order, before falling back to <c>PATH</c>.</summary>
    public required IReadOnlyList<string> SearchDirectories { get; init; }

    /// <summary>Appended to every tool name before resolving — <c>""</c> on Linux,
    /// <c>".exe"</c> on Windows.</summary>
    public required string ExecutableSuffix { get; init; }

    /// <summary>Root directory under which every tool's on-disk cache lives (each
    /// tool gets its own subfolder under this — see the composition root). A machine
    /// fact (derived from <c>Environment.GetFolderPath</c>), not adapter policy.</summary>
    public required string CacheRoot { get; init; }

    /// <summary>Every tool name this build knows about. <see cref="ToolSet"/>
    /// resolves a binary path for each of these at construction. Adding a tool is one
    /// const in <see cref="ExternalToolNames"/> plus one entry in
    /// <see cref="ExternalToolCatalog.All"/> — nothing here needs to change.</summary>
    public required IReadOnlyList<string> Tools { get; init; }
}
