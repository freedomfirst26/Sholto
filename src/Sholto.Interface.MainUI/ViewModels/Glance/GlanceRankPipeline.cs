using Microsoft.Extensions.Options;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Ranking for Glance. A change the person makes (query, chips, target, open) ranks at once; several
/// may be in flight and only the newest result is shown. A change that arrives as an event (a summary, the
/// rows, a deck) only marks the list dirty, and one more ranking runs on the next frame once none is in flight.
/// <para>The slow indicator (<see cref="IsReassessing"/>) comes on when a ranking the person asked for has not
/// landed after 150 ms, and once on it stays at least 300 ms: a result that arrives sooner is held back until
/// then. Rankings the person did not ask for never start it.</para></summary>
public sealed class GlanceRankPipeline : IGlanceRankPipeline
{
    private readonly IQueryAsker _asker;
    private readonly IAppThread _appThread;
    private readonly IFrameClock _clock;
    private readonly int _indicatorDelayMs;
    private readonly int _indicatorMinimumMs;

    private IReadOnlyList<GlanceChip> _appliedChips = [];
    private string _appliedQuery = "";
    private DateTime? _staleSince;
    private DateTime _shownAt;
    private (RankTracks Request, IReadOnlyList<GlanceChip> Chips, RankedTracks Ranked)? _held;
    private int _rankGen;
    private int _inFlight;
    private bool _dirty;

    public GlanceRankPipeline(IQueryAsker asker, IAppThread appThread, IFrameClock clock,
        IOptions<GlanceViewOptions> options)
    {
        _asker = asker;
        _appThread = appThread;
        _clock = clock;
        var view = options.Value;
        _indicatorDelayMs = view.IndicatorDelayMs;
        _indicatorMinimumMs = view.IndicatorMinimumMs;
    }

    public event Action? ReassessingChanged;

    public event Action<RankTracks, RankedTracks>? Shown;

    public bool IsReassessing { get; private set; }

    public bool ScopeChipsActive { get; private set; }

    public int ResultScopeCount { get; private set; }

    public IReadOnlyDictionary<int, int>? CrateCounts { get; private set; }

    public IReadOnlyDictionary<string, int>? TagCounts { get; private set; }

    public string? ScopeEmptyText => ScopeEmptyTextFor(_appliedChips, _appliedQuery);

    public void MarkDirty() => _dirty = true;

    /// <summary>Ask for a ranking. Only one the person asked for can start the slow indicator's clock, so a
    /// fader sweep re-ranking a big library never makes it flicker.</summary>
    public void Rank(string query, int target, IReadOnlyList<GlanceChip> chips, bool personInitiated)
    {
        _dirty = false;
        _held = null;
        if (personInitiated) _staleSince ??= _clock.Now;
        var gen = ++_rankGen;
        _inFlight++;
        var request = new RankTracks(query, target)
        {
            CrateIds = [.. chips.Where(c => c.Kind == GlanceChipKind.Crate).Select(c => c.CrateId)],
            Tags = [.. chips.Where(c => c.Kind == GlanceChipKind.Tag).Select(c => c.Name)],
        };
        _ = RankAsync(gen, request, chips);
    }

    public bool Tick(DateTime now)
    {
        if (_held is not null && IsReassessing && (now - _shownAt).TotalMilliseconds >= _indicatorMinimumMs)
        {
            FlushHeld();
            SetReassessing(false);
        }
        if (_staleSince is { } since && !IsReassessing && (now - since).TotalMilliseconds >= _indicatorDelayMs)
        {
            SetReassessing(true);
            _shownAt = now;
        }
        return _dirty && _inFlight == 0;
    }

    public void Close()
    {
        FlushHeld();
        _staleSince = null;
        SetReassessing(false);
    }

    private async Task RankAsync(int gen, RankTracks request, IReadOnlyList<GlanceChip> chips)
    {
        RankedTracks? result = null;
        try { result = await _asker.AskAsync<RankTracks, RankedTracks>(request); }
        catch (Exception ex) { Console.WriteLine($"[Glance] ranking failed: {ex.Message}"); }
        _appThread.Post(() => Apply(gen, request, chips, result));
    }

    private void Apply(int gen, RankTracks request, IReadOnlyList<GlanceChip> chips, RankedTracks? ranked)
    {
        _inFlight--;
        if (gen != _rankGen) return;
        _staleSince = null;
        if (ranked is null)
        {
            SetReassessing(false);
            return;
        }
        if (IsReassessing && (_clock.Now - _shownAt).TotalMilliseconds < _indicatorMinimumMs)
        {
            _held = (request, chips, ranked);
            return;
        }
        Show(request, chips, ranked);
        SetReassessing(false);
    }

    private void FlushHeld()
    {
        if (_held is not { } held) return;
        _held = null;
        Show(held.Request, held.Chips, held.Ranked);
    }

    private void Show(RankTracks request, IReadOnlyList<GlanceChip> chips, RankedTracks ranked)
    {
        _appliedChips = chips;
        _appliedQuery = request.Query;
        ScopeChipsActive = _appliedChips.Count > 0;
        ResultScopeCount = ranked.ScopeCount;
        CrateCounts = ranked.ScopeCrateCounts is { } cc ? new Dictionary<int, int>(cc) : null;
        TagCounts = ranked.ScopeTagCounts is { } tc ? new Dictionary<string, int>(tc, StringComparer.OrdinalIgnoreCase) : null;
        Shown?.Invoke(request, ranked);
    }

    private void SetReassessing(bool value)
    {
        if (IsReassessing == value) return;
        IsReassessing = value;
        ReassessingChanged?.Invoke();
    }

    /// <summary>"No tracks in Peak time tagged Vocal", or with text "No “bsn” in Peak time tagged Vocal".</summary>
    private string? ScopeEmptyTextFor(IReadOnlyList<GlanceChip> chips, string query)
    {
        if (chips.Count == 0) return null;
        var crates = chips.Where(c => c.Kind == GlanceChipKind.Crate).Select(c => c.Name).ToList();
        var tags = chips.Where(c => c.Kind == GlanceChipKind.Tag).Select(c => c.Name).ToList();
        var text = query.Trim();
        var text0 = text.Length == 0 ? "No tracks" : $"No “{text}”";
        var result = text0;
        if (crates.Count > 0) result += " in " + JoinNames(crates);
        if (tags.Count > 0) result += " tagged " + JoinNames(tags);
        return result;
    }

    private string JoinNames(List<string> names) => names.Count == 1
        ? names[0]
        : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
}
