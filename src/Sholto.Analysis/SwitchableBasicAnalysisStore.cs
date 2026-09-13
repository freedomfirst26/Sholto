using Sholto.Analysis.Analyzers;

namespace Sholto.Analysis;

/// <summary>
/// Forwards to whatever <see cref="IBasicAnalysisStore"/> is currently attached — a
/// permanent miss until <see cref="Attach"/> is called once. Same swap pattern as
/// <see cref="SwitchableKeyAnalysisStore"/>, applied to the DB tier of
/// <see cref="AnalysisProvider"/>'s cache list.
///
/// This is what replaces the old <c>Func&lt;AnalysisProvider&gt;</c> the composition
/// root used to hand <c>DeckFactory</c>: because <see cref="AnalysisProvider"/>'s
/// cache list is just data (no deck-specific state), ONE <see cref="AnalysisProvider"/>
/// — built once, over <c>[memoryCache, switchableBasicCache]</c> — can be shared by
/// both decks instead of being rebuilt per-deck by a closure. The DB tier still
/// starts absent (miss-always) and becomes real the moment the composition root
/// calls <see cref="Attach"/> after the DB opens; every deck already holding the
/// provider sees the upgrade on its very next lookup, with no closure over a
/// nullable field involved anywhere.
/// </summary>
public sealed class SwitchableBasicAnalysisStore : IBasicAnalysisStore
{
    private IBasicAnalysisStore? _target;

    public string Name => _target?.Name ?? "unattached";

    /// <summary>Point at the real (DB-backed) store. Call once, after the DB opens.</summary>
    public void Attach(IBasicAnalysisStore real) => _target = real;

    public Task<BasicAnalysis?> TryGetAsync(string filePath) =>
        _target?.TryGetAsync(filePath) ?? Task.FromResult<BasicAnalysis?>(null);

    public Task PutAsync(string filePath, BasicAnalysis analysis) =>
        _target?.PutAsync(filePath, analysis) ?? Task.CompletedTask;
}
