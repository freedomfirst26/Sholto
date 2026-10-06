using Sholto.Data;

namespace Sholto.Interface.MainUI.Harness.Ui;

/// <summary>
/// The queries Glance asks, as the harness answers them: the real bus answers every one (the rail's crates and tags
/// come from the demo database, see <see cref="IDemoLibrary"/>), but a ranking can be held back to show the slow
/// indicator. Dev-only; never part of the app.
/// </summary>
/// <param name="inner">The bus the real handlers answer on.</param>
/// <param name="rankDelayMs">How long a ranking is held before its answer lands (0: not at all). The harness reads it
/// from <c>SHOLTO_HARNESS_SLOWRANK</c>.</param>
public sealed class DemoGlanceAsker(IQueryAsker inner, int rankDelayMs) : IQueryAsker
{
    private readonly IQueryAsker _inner = inner;
    private readonly int _rankDelayMs = rankDelayMs;

    public TR Ask<TQ, TR>(in TQ query) where TQ : struct, IQuery<TR>
    {
        if (query is RankTracks && _rankDelayMs > 0)
            return (TR)(object)Held((Task<RankedTracks>)(object)_inner.Ask<TQ, TR>(in query)!);
        return _inner.Ask<TQ, TR>(in query);
    }

    private async Task<RankedTracks> Held(Task<RankedTracks> ranking)
    {
        await Task.Delay(_rankDelayMs);
        return await ranking;
    }
}
