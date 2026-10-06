using Microsoft.Extensions.Options;
using Sholto.App.Decks;
using Sholto.App.Library;
using Sholto.App.Library.Crates;
using Sholto.Data;

namespace Sholto.App.Glance;

/// <summary>Answers <see cref="RankTracks"/>. On the caller's (app) thread it copies the whole catalogue (not just the
/// visible rows) and reads the reference from the deck that is not the target; the chip scoping, filtering, scoring and
/// sorting then run on the thread pool, off those copies. The chips (crates and tags) are the only narrowing besides the
/// text. The reference is null (fit off) when that deck holds no track. The rows are capped at
/// <see cref="GlanceOptions.MaxResults"/>; <see cref="RankedTracks.ScopeCount"/> keeps the full scope size.</summary>
public sealed class RankTracksHandler(
    ILibrarySession library, IDecks decks, IGlanceRanker ranker, IGlanceScope scope,
    ICrateMembershipCache membership, IOptions<GlanceOptions> options) :
    IQueryHandler<RankTracks, Task<RankedTracks>>
{
    private readonly ILibrarySession _library = library;
    private readonly IDecks _decks = decks;
    private readonly IGlanceRanker _ranker = ranker;
    private readonly IGlanceScope _scope = scope;
    private readonly ICrateMembershipCache _membership = membership;
    private readonly GlanceOptions _options = options.Value;

    public Task<RankedTracks> Handle(in RankTracks query)
    {
        var catalogue = _library.Catalog.ToArray();
        var reference = ReferenceFor(query.TargetDeck == 0 ? 1 : 0);
        var crateIds = query.CrateIds ?? [];
        var tags = query.Tags ?? [];
        var hasChips = crateIds.Count > 0 || tags.Count > 0;
        // Only a chip needs membership; asked here, on the app thread, so the cache stays single-threaded.
        var pending = hasChips ? _membership.CurrentAsync() : null;
        return RankAsync(catalogue, reference, query.Query, crateIds, tags, pending);
    }

    private async Task<RankedTracks> RankAsync(
        TrackSummary[] catalogue, GlanceReference? reference, string text,
        IReadOnlyList<int> crateIds, IReadOnlyList<string> tags, Task<CrateMembership>? pending)
    {
        var members = pending is null ? null : await pending;
        return await Task.Run(() =>
        {
            var scoped = members is null
                ? new GlanceScopeResult(catalogue, null, null)
                : _scope.Apply(catalogue, members, crateIds, tags);
            var ranked = _ranker.Rank(scoped.Rows, reference, text);
            return ranked with
            {
                Rows = ranked.Rows.Take(_options.MaxResults).ToList(),
                ScopeCount = scoped.Rows.Count,
                ScopeCrateCounts = scoped.CrateCounts,
                ScopeTagCounts = scoped.TagCounts,
            };
        });
    }

    private GlanceReference? ReferenceFor(int deckIndex)
    {
        var deck = _decks.DeckFor(deckIndex);
        if (deck.LoadedTrack is not { } track) return null;
        var bpm = deck.EffectiveBpm;
        return new GlanceReference(deckIndex, deck.LoadedKey.ToRef(), bpm > 0 ? bpm : null, track.FilePath);
    }
}
