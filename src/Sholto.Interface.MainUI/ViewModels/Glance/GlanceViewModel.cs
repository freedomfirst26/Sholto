using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Port of the mockup's Glance overlay onto the bus. Ranking is the App's (<see cref="RankTracks"/>);
/// this class keeps the query, the target deck, the zone and the highlight, tells the App which track the
/// highlight points at (<see cref="SetSearchPick"/>) so the controller's LOAD and knob follow it, and shows
/// the answers.
/// <para>A change the person makes (query, chips, target, open) ranks at once; several may be in flight and only
/// the newest result is shown. A change that arrives as an event (a summary, the rows, a deck) only marks the
/// list dirty, and one more ranking runs on the next frame once none is in flight.</para>
/// <para>The slow indicator (<see cref="IsReassessing"/>) comes on when a ranking the person asked for has not
/// landed after 150 ms, and once on it stays at least 300 ms: a result that arrives sooner is held back until
/// then. Rankings the person did not ask for never start it.</para></summary>
public sealed class GlanceViewModel :
    IGlanceViewModel,
    IFrameTickHandler,
    IEventHandler<SearchRequested>,
    IEventHandler<SearchCursorMoved>,
    IEventHandler<LoadConfirmPending>,
    IEventHandler<LoadAccepted>,
    IEventHandler<LibraryRowsChanged>,
    IEventHandler<TrackSummaryChanged>,
    IEventHandler<ShortlistChanged>,
    IEventHandler<RecentLoadsChanged>,
    IEventHandler<LibraryDatabaseAttached>,
    IEventHandler<DeckContentChanged>,
    IEventHandler<DeckTempoChanged>
{
    /// <summary>Frame order for the re-rank tick.</summary>
    public const int ClockOrder = 100;

    private const int RailLimit = 10;

    /// <summary>How long a person-asked ranking may run before the indicator shows.</summary>
    private const int IndicatorDelayMs = 150;

    /// <summary>The shortest time the indicator stays on once shown.</summary>
    private const int IndicatorMinimumMs = 300;

    private readonly ICommandSender _sender;
    private readonly IQueryAsker _asker;
    private readonly IAppThread _appThread;
    private readonly IGlanceRowSource _rowSource;
    private readonly ITagRecency _tagRecency;
    private readonly IFrameClock _clock;
    private readonly IMotionPreference _motion;

    private IReadOnlyList<TrackSummary> _shortlist = [];
    private HashSet<string> _shortlistPaths = [];
    private IReadOnlyList<TrackSummary> _recent = [];
    private IReadOnlyList<CrateRef> _crates = [];
    private IReadOnlyList<TagHit> _tags = [];
    private bool _databaseUp;

    private IReadOnlyList<GlanceChip> _chips = [];
    private IReadOnlyList<GlanceChip> _appliedChips = [];
    private string _appliedQuery = "";
    private int _libraryCount;
    private int _resultScopeCount;
    private Dictionary<int, int>? _crateCounts;
    private Dictionary<string, int>? _tagCounts;

    private DateTime? _staleSince;
    private DateTime _shownAt;
    private (RankTracks Request, IReadOnlyList<GlanceChip> Chips, RankedTracks Ranked)? _held;

    private int _rankGen;
    private int _inFlight;
    private bool _dirty;
    private bool _resetHighlight;
    private int _railGen;

    private string? _completionToken;
    private string? _completionTag;
    private int _completionGen;

    private bool _sentActive;
    private string? _sentPath;

    public GlanceViewModel(
        ICommandSender sender, IQueryAsker asker, IEventSubscriber subscriber, IAppThread appThread,
        IFrameClock clock, IGlanceRowSource rowSource, ITagRecency tagRecency, IGlanceHeaderViewModel header,
        IMotionPreference motion)
    {
        _sender = sender;
        _asker = asker;
        _appThread = appThread;
        _rowSource = rowSource;
        _tagRecency = tagRecency;
        _clock = clock;
        _motion = motion;
        Header = header;
        clock.Subscribe(this, ClockOrder);
        subscriber.Subscribe<SearchRequested>(this);
        subscriber.Subscribe<SearchCursorMoved>(this);
        subscriber.Subscribe<LoadConfirmPending>(this);
        subscriber.Subscribe<LoadAccepted>(this);
        subscriber.Subscribe<LibraryRowsChanged>(this);
        subscriber.Subscribe<TrackSummaryChanged>(this);
        subscriber.Subscribe<ShortlistChanged>(this);
        subscriber.Subscribe<RecentLoadsChanged>(this);
        subscriber.Subscribe<LibraryDatabaseAttached>(this);
        subscriber.Subscribe<DeckContentChanged>(this);
        subscriber.Subscribe<DeckTempoChanged>(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? SelectAllOnOpen;

    public event Action? ResultsReplaced;

    public IGlanceHeaderViewModel Header { get; }

    public bool IsOpen { get; private set; }

    private bool _scopeChipsActive;

    private string _query = "";
    public string Query
    {
        get => _query;
        set
        {
            if (_query == value) return;
            var railChanged = RailText(_query) != RailText(value);
            _query = value;
            Notify();
            if (!IsOpen) return;
            _resetHighlight = true;
            TableIndex = 0;
            Rank(personInitiated: true);
            if (railChanged) RefreshRail();
            RefreshCompletion();
        }
    }

    public string? CompletionSuffix { get; private set; }

    public int Target { get; private set; }

    public IReadOnlyList<GlanceRow> Rows { get; private set; } = [];

    public int ScopeCount => _scopeChipsActive || _resultScopeCount > 0 ? _resultScopeCount : _libraryCount;

    public IReadOnlyList<GlanceChip> Chips => _chips;

    public bool IsReassessing { get; private set; }

    public bool AnimateResults => !_motion.Reduced;

    public string? ScopeEmptyText => ScopeEmptyTextFor(_appliedChips, _appliedQuery);

    public IReadOnlyList<string> FilterChips { get; private set; } = [];

    public GlanceZone Zone { get; private set; }

    public int TableIndex { get; private set; }

    public IReadOnlyList<object> RailItems { get; private set; } = [];

    public int RailIndex { get; private set; }

    public string? HighlightedPath => Zone == GlanceZone.Table
        ? TableIndex >= 0 && TableIndex < Rows.Count ? Rows[TableIndex].FilePath : null
        : RailIndex >= 0 && RailIndex < RailItems.Count && RailItems[RailIndex] is GlanceRailTrack t ? t.FilePath : null;

    public string ActionText => Zone == GlanceZone.Table
        ? HighlightedPath is null ? "" : $"Load to Deck {Target + 1}"
        : RailIndex >= 0 && RailIndex < RailItems.Count
            ? RailItems[RailIndex] switch
            {
                GlanceRailTrack => $"Load to Deck {Target + 1}",
                GlanceRailCrate { IsActive: true } or GlanceRailTag { IsActive: true } => "Remove filter",
                GlanceRailCrate or GlanceRailTag => "Add filter",
                _ => "",
            }
            : "";

    public string AlternateActionText => Zone == GlanceZone.Rail && HighlightedRailFilter() is not null ? "Show in library" : "";

    // ---- Open, close, target ------------------------------------------------------------------

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        Notify(nameof(IsOpen));
        Header.SetOpen(true);
        SetZone(GlanceZone.Table);
        var suggested = _asker.Ask<SuggestLoadTarget, int>(new SuggestLoadTarget());
        Target = suggested == 1 ? 1 : 0;
        Notify(nameof(Target));
        _resetHighlight = true;
        TableIndex = 0;
        RailIndex = FirstSelectable();
        SelectAllOnOpen?.Invoke();
        Rank(personInitiated: true);
        RefreshRail();
        NotifyHighlight();
        SyncPick();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Notify(nameof(IsOpen));
        FlushHeld();
        _staleSince = null;
        SetReassessing(false);
        Header.SetOpen(false);
        SyncPick();
    }

    public void FlipTarget() => SetTarget(1 - Target);

    public void SetTarget(int deck) => ChangeTarget(deck, resetHighlight: true);

    /// <summary>Aim at <paramref name="deck"/> and re-rank. With <paramref name="resetHighlight"/> false the
    /// highlight stays on the same track through the re-rank, which is what a load or a pending confirmation
    /// needs: the track the person pointed at is the track a second press must see again.</summary>
    private void ChangeTarget(int deck, bool resetHighlight)
    {
        deck = deck == 1 ? 1 : 0;
        if (deck == Target) return;
        Target = deck;
        Notify(nameof(Target));
        Notify(nameof(ActionText));
        Notify(nameof(AlternateActionText));
        if (!IsOpen) return;
        if (resetHighlight)
        {
            _resetHighlight = true;
            TableIndex = 0;
        }
        Rank(personInitiated: true);
    }

    // ---- Navigation ----------------------------------------------------------------------------

    public void Move(int delta)
    {
        if (!IsOpen || delta == 0) return;
        if (Zone == GlanceZone.Table)
        {
            var next = Rows.Count == 0 ? 0 : Math.Clamp(TableIndex + delta, 0, Rows.Count - 1);
            if (next == TableIndex) return;
            TableIndex = next;
            Notify(nameof(TableIndex));
        }
        else
        {
            var step = delta > 0 ? 1 : -1;
            var index = RailIndex;
            for (var n = Math.Abs(delta); n > 0; n--)
            {
                var candidate = index + step;
                while (candidate >= 0 && candidate < RailItems.Count && RailItems[candidate] is GlanceRailHeader)
                    candidate += step;
                if (candidate < 0 || candidate >= RailItems.Count) break;
                index = candidate;
            }
            if (index == RailIndex) return;
            RailIndex = index;
            Notify(nameof(RailIndex));
        }
        NotifyHighlight();
        SyncPick();
    }

    public void ToggleZone()
    {
        if (!IsOpen) return;
        SetZone(Zone == GlanceZone.Table ? GlanceZone.Rail : GlanceZone.Table);
        NotifyHighlight();
        SyncPick();
    }

    private void SetZone(GlanceZone zone)
    {
        if (Zone == zone) return;
        Zone = zone;
        Notify(nameof(Zone));
    }

    // ---- Actions -------------------------------------------------------------------------------

    public void Activate()
    {
        if (!IsOpen) return;
        if (Zone == GlanceZone.Rail && HighlightedRailFilter() is not null)
        {
            ActivateRailItem();
            return;
        }
        LoadHighlighted(Target);
    }

    public void ActivateRailItem()
    {
        if (!IsOpen || HighlightedRailFilter() is not { } chip) return;
        var existing = _chips.FirstOrDefault(c => SameFilter(c, chip));
        if (existing is not null)
        {
            _chips = [.. _chips.Where(c => !ReferenceEquals(c, existing))];
        }
        else
        {
            if (chip.Kind == GlanceChipKind.Tag) _tagRecency.MarkUsed(chip.Name);
            _chips = [.. _chips, chip];
            ClearFreeWords();
        }
        ChipsChanged();
    }

    public void ActivateAlternate()
    {
        if (!IsOpen) return;
        if (Zone == GlanceZone.Rail && HighlightedRailFilter() is { } chip)
        {
            if (chip.Kind == GlanceChipKind.Crate)
                _sender.Send(new FilterLibraryByCrate(chip.CrateId, chip.Name, Ui("activate-crate")));
            else
            {
                _tagRecency.MarkUsed(chip.Name);
                _sender.Send(new FilterLibraryByTag(chip.Name, Ui("activate-tag")));
            }
            Close();
            return;
        }
        Activate();
    }

    public bool AcceptTagCompletion()
    {
        if (!IsOpen || _completionTag is not { } tag || _completionToken is not { } token
            || TrailingTagToken(_query) != token) return false;
        _query = _query[..^token.Length].TrimEnd();
        Notify(nameof(Query));
        SetCompletion(null, null, null);
        _completionGen++;
        _tagRecency.MarkUsed(tag);
        _chips = [.. _chips, new GlanceChip(GlanceChipKind.Tag, 0, tag)];
        ChipsChanged();
        return true;
    }

    public bool RemoveLastChip()
    {
        if (_query.Length > 0 || _chips.Count == 0) return false;
        _chips = [.. _chips.Take(_chips.Count - 1)];
        ChipsChanged();
        return true;
    }

    public void RemoveChip(GlanceChip chip)
    {
        var existing = _chips.FirstOrDefault(c => SameFilter(c, chip));
        if (existing is null) return;
        _chips = [.. _chips.Where(c => !ReferenceEquals(c, existing))];
        ChipsChanged();
    }

    /// <summary>A chip change ranks at once from the top of the list; the rail's active marks follow now, its
    /// counts follow the result.</summary>
    private void ChipsChanged()
    {
        Notify(nameof(Chips));
        _resetHighlight = true;
        TableIndex = 0;
        Notify(nameof(TableIndex));
        if (IsOpen) Rank(personInitiated: true);
        RebuildRail();
        RefreshRail();
        RefreshCompletion();
        NotifyHighlight();
    }

    /// <summary>The query without its plain words; <c>bpm:</c>, <c>key:</c> and <c>#</c> tokens stay.</summary>
    private void ClearFreeWords()
    {
        var kept = string.Join(' ', _query
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.StartsWith('#')
                        || w.StartsWith("bpm:", StringComparison.OrdinalIgnoreCase)
                        || w.StartsWith("key:", StringComparison.OrdinalIgnoreCase)));
        if (kept == _query) return;
        _query = kept;
        Notify(nameof(Query));
    }

    // ---- Tag completion ------------------------------------------------------------------------

    /// <summary>The last word of the query when it is a <c>#</c> fragment of at least two characters and the
    /// query does not end in whitespace; otherwise null.</summary>
    private string? TrailingTagToken(string query)
    {
        if (query.Length == 0 || char.IsWhiteSpace(query[^1])) return null;
        var word = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[^1];
        return word.StartsWith('#') && word.Length >= 2 ? word : null;
    }

    private void SetCompletion(string? token, string? tag, string? suffix)
    {
        _completionToken = token;
        _completionTag = tag;
        if (CompletionSuffix == suffix) return;
        CompletionSuffix = suffix;
        Notify(nameof(CompletionSuffix));
    }

    private void RefreshCompletion()
    {
        var gen = ++_completionGen;
        var token = TrailingTagToken(_query);
        if (token is null || !_databaseUp)
        {
            SetCompletion(null, null, null);
            return;
        }
        var fragment = token[1..];
        if (_completionTag is { } kept && kept.StartsWith(fragment, StringComparison.OrdinalIgnoreCase))
            SetCompletion(token, kept, kept[fragment.Length..]);
        else
            SetCompletion(token, null, null);
        _ = RefreshCompletionAsync(gen, token, fragment);
    }

    private async Task RefreshCompletionAsync(int gen, string token, string fragment)
    {
        IReadOnlyList<TagHit> hits = [];
        try
        {
            hits = await _asker.AskAsync<SearchTags, IReadOnlyList<TagHit>>(new SearchTags(fragment, RailLimit)) ?? [];
        }
        catch (Exception ex) { Console.WriteLine($"[Glance] tag completion failed: {ex.Message}"); }
        _appThread.Post(() =>
        {
            if (gen != _completionGen || TrailingTagToken(_query) != token) return;
            var tag = _tagRecency.OrderRecentFirst(
                    hits.Where(h => h.Name.StartsWith(fragment, StringComparison.OrdinalIgnoreCase)
                                    && !_chips.Any(c => c.Kind == GlanceChipKind.Tag
                                                        && string.Equals(c.Name, h.Name, StringComparison.OrdinalIgnoreCase))),
                    h => h.Name)
                .FirstOrDefault()?.Name;
            SetCompletion(token, tag, tag?[fragment.Length..]);
        });
    }

    private bool SameFilter(GlanceChip a, GlanceChip b) => a.Kind == b.Kind
        && (a.Kind == GlanceChipKind.Crate
            ? a.CrateId == b.CrateId
            : string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The chip the highlighted rail item would add or remove, or null on anything else.</summary>
    private GlanceChip? HighlightedRailFilter() =>
        RailIndex >= 0 && RailIndex < RailItems.Count
            ? RailItems[RailIndex] switch
            {
                GlanceRailCrate c => new GlanceChip(GlanceChipKind.Crate, c.Id, c.Name),
                GlanceRailTag t => new GlanceChip(GlanceChipKind.Tag, 0, t.Name),
                _ => null,
            }
            : null;

    public void LoadTo(int deck)
    {
        if (!IsOpen) return;
        deck = deck == 1 ? 1 : 0;
        ChangeTarget(deck, resetHighlight: false);
        LoadHighlighted(deck);
    }

    private void LoadHighlighted(int deck)
    {
        if (HighlightedPath is null) return;
        SyncPick();
        _sender.Send(new LoadSelectedIntoDeck(deck, Ui("load")));
    }

    public void ToggleShortlistOnHighlight()
    {
        if (!IsOpen || HighlightedPath is not { } path) return;
        _sender.Send(new ToggleShortlist(path, Ui("shortlist")));
    }

    public bool TryShortlistKey()
    {
        if (_query.Length > 0 || !IsOpen) return false;
        ToggleShortlistOnHighlight();
        return true;
    }

    // ---- Events --------------------------------------------------------------------------------

    public void Handle(in SearchRequested e)
    {
        if (IsOpen) ToggleZone();
        else Open();
    }

    public void Handle(in SearchCursorMoved e) => Move(e.Delta);

    public void Handle(in LoadConfirmPending e)
    {
        if (e.Pending) ChangeTarget(e.Deck, resetHighlight: false);
    }

    public void Handle(in LoadAccepted e) => Close();

    public void Handle(in LibraryRowsChanged e)
    {
        _libraryCount = e.Rows.Count;
        NotifyScopeCount();
        _dirty = true;
    }

    public void Handle(in TrackSummaryChanged e) => _dirty = true;

    public void Handle(in DeckContentChanged e) => _dirty = true;

    public void Handle(in DeckTempoChanged e) => _dirty = true;

    public void Handle(in ShortlistChanged e)
    {
        _shortlist = e.Tracks;
        _shortlistPaths = [.. e.Tracks.Select(t => t.FilePath)];
        foreach (var row in Rows) row.IsShortlisted = _shortlistPaths.Contains(row.FilePath);
        RebuildRail();
    }

    public void Handle(in RecentLoadsChanged e)
    {
        _recent = e.Tracks;
        RebuildRail();
    }

    public void Handle(in LibraryDatabaseAttached e)
    {
        _databaseUp = e.Available;
        if (_databaseUp && IsOpen) RefreshRail();
    }

    public void OnFrame(DateTime now)
    {
        if (!IsOpen) return;
        if (_held is not null && IsReassessing && (now - _shownAt).TotalMilliseconds >= IndicatorMinimumMs)
        {
            FlushHeld();
            SetReassessing(false);
        }
        if (_staleSince is { } since && !IsReassessing && (now - since).TotalMilliseconds >= IndicatorDelayMs)
        {
            SetReassessing(true);
            _shownAt = now;
        }
        if (!_dirty || _inFlight > 0) return;
        Rank(personInitiated: false);
    }

    // ---- Ranking -------------------------------------------------------------------------------

    /// <summary>Ask for a ranking. Only one the person asked for can start the slow indicator's clock, so a
    /// fader sweep re-ranking a big library never makes it flicker.</summary>
    private void Rank(bool personInitiated)
    {
        _dirty = false;
        _held = null;
        if (personInitiated) _staleSince ??= _clock.Now;
        var gen = ++_rankGen;
        _inFlight++;
        var request = new RankTracks(_query, Target)
        {
            CrateIds = [.. _chips.Where(c => c.Kind == GlanceChipKind.Crate).Select(c => c.CrateId)],
            Tags = [.. _chips.Where(c => c.Kind == GlanceChipKind.Tag).Select(c => c.Name)],
        };
        _ = RankAsync(gen, request, _chips);
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
        if (IsReassessing && (_clock.Now - _shownAt).TotalMilliseconds < IndicatorMinimumMs)
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
        var previous = _resetHighlight ? null : HighlightedTablePath();
        var rows = new List<GlanceRow>(ranked.Rows.Count);
        foreach (var r in ranked.Rows)
            rows.Add(new GlanceRow(_rowSource.RowFor(r.Summary), r, _shortlistPaths.Contains(r.Summary.FilePath)));
        var index = 0;
        if (previous is not null)
        {
            var found = rows.FindIndex(r => r.FilePath == previous);
            if (found >= 0) index = found;
        }
        _resetHighlight = false;

        _scopeChipsActive = _appliedChips.Count > 0;
        _resultScopeCount = ranked.ScopeCount;
        _crateCounts = ranked.ScopeCrateCounts is { } cc ? new Dictionary<int, int>(cc) : null;
        _tagCounts = ranked.ScopeTagCounts is { } tc ? new Dictionary<string, int>(tc, StringComparer.OrdinalIgnoreCase) : null;

        Rows = rows;
        FilterChips = ranked.FilterChips;
        TableIndex = index;
        Header.Apply(ranked, request.TargetDeck);
        Notify(nameof(Rows));
        Notify(nameof(FilterChips));
        Notify(nameof(TableIndex));
        NotifyScopeCount();
        Notify(nameof(ScopeEmptyText));
        RebuildRail();
        NotifyHighlight();
        SyncPick();
        ResultsReplaced?.Invoke();
    }

    private void SetReassessing(bool value)
    {
        if (IsReassessing == value) return;
        IsReassessing = value;
        Notify(nameof(IsReassessing));
    }

    private void NotifyScopeCount()
    {
        Notify(nameof(ScopeCount));
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

    private string? HighlightedTablePath() =>
        TableIndex >= 0 && TableIndex < Rows.Count ? Rows[TableIndex].FilePath : null;

    // ---- Rail ----------------------------------------------------------------------------------

    private void RefreshRail()
    {
        var gen = ++_railGen;
        if (!_databaseUp)
        {
            _crates = [];
            _tags = [];
            RebuildRail();
            return;
        }
        _ = RefreshRailAsync(gen, RailText(_query));
    }

    private async Task RefreshRailAsync(int gen, string text)
    {
        IReadOnlyList<CrateRef> crates = [];
        IReadOnlyList<TagHit> tags = [];
        try
        {
            crates = await _asker.AskAsync<SearchCrates, IReadOnlyList<CrateRef>>(new SearchCrates(text)) ?? [];
            if (text.Length == 0)
            {
                // Tags picked earlier this session lead, newest first; the most-used tags fill the rest.
                var recentNames = _tagRecency.RecentNames(RailLimit);
                var recentHits = await _asker.AskAsync<TagsByName, IReadOnlyList<TagHit>>(new TagsByName(recentNames)) ?? [];
                var byName = recentHits.ToDictionary(h => h.Name, StringComparer.OrdinalIgnoreCase);
                var ordered = recentNames.Where(byName.ContainsKey).Select(n => byName[n]);
                var top = await _asker.AskAsync<TopTags, IReadOnlyList<TagHit>>(new TopTags(RailLimit)) ?? [];
                tags = [.. ordered.Concat(top).DistinctBy(h => h.Name, StringComparer.OrdinalIgnoreCase).Take(RailLimit)];
            }
            else
            {
                var matches = await _asker.AskAsync<SearchTags, IReadOnlyList<TagHit>>(new SearchTags(text, RailLimit)) ?? [];
                tags = _tagRecency.OrderRecentFirst(matches, h => h.Name);
            }
        }
        catch (Exception ex) { Console.WriteLine($"[Glance] rail search failed: {ex.Message}"); }
        _appThread.Post(() =>
        {
            if (gen != _railGen) return;
            _crates = crates;
            _tags = tags;
            RebuildRail();
        });
    }

    private void RebuildRail()
    {
        var selected = RailIndex >= 0 && RailIndex < RailItems.Count ? RailKey(RailItems[RailIndex]) : null;
        var items = new List<object>
        {
            new GlanceRailHeader("SHORTLIST", _shortlist.Count),
        };
        items.AddRange(_shortlist.Select(RailTrack));
        items.Add(new GlanceRailHeader("RECENT LOADS", _recent.Count));
        items.AddRange(_recent.Select(RailTrack));
        items.Add(new GlanceRailHeader("CRATES", _crates.Count));
        items.AddRange(_crates.Select(RailCrate));
        items.Add(new GlanceRailHeader("TAGS", _tags.Count));
        items.AddRange(_tags.Select(RailTag));
        RailItems = items;

        var index = selected is null ? -1 : items.FindIndex(i => RailKey(i) == selected);
        RailIndex = index >= 0 ? index : FirstSelectable();
        Notify(nameof(RailItems));
        Notify(nameof(RailIndex));
        NotifyHighlight();
        SyncPick();
    }

    private GlanceRailCrate RailCrate(CrateRef crate)
    {
        var active = _chips.Any(c => c.Kind == GlanceChipKind.Crate && c.CrateId == crate.Id);
        var count = _crateCounts is { } counts ? counts.GetValueOrDefault(crate.Id) : crate.TrackCount;
        return new GlanceRailCrate(crate, active, count, count == 0 && !active);
    }

    private GlanceRailTag RailTag(TagHit tag)
    {
        var active = _chips.Any(c => c.Kind == GlanceChipKind.Tag
                                     && string.Equals(c.Name, tag.Name, StringComparison.OrdinalIgnoreCase));
        var count = _tagCounts is { } counts ? counts.GetValueOrDefault(tag.Name) : tag.TrackCount;
        return new GlanceRailTag(tag, active, count, count == 0 && !active);
    }

    private GlanceRailTrack RailTrack(TrackSummary summary) => new(summary, _rowSource.RowFor(summary.FilePath));

    private string? RailKey(object item) => item switch
    {
        GlanceRailTrack t => "t:" + t.FilePath,
        GlanceRailCrate c => "c:" + c.Id,
        GlanceRailTag g => "g:" + g.Name,
        _ => null,
    };

    private int FirstSelectable()
    {
        for (var i = 0; i < RailItems.Count; i++)
            if (RailItems[i] is not GlanceRailHeader) return i;
        return 0;
    }

    /// <summary>The free words of the query: what a crate or a tag is matched against. Operators such as
    /// <c>bpm:128</c>, <c>key:8A</c> and <c>#techno</c> are left out.</summary>
    private string RailText(string query) => string.Join(' ', query
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        .Where(w => !w.StartsWith('#')
                    && !w.StartsWith("bpm:", StringComparison.OrdinalIgnoreCase)
                    && !w.StartsWith("key:", StringComparison.OrdinalIgnoreCase)));

    // ---- Pick sync -----------------------------------------------------------------------------

    /// <summary>Tell the App what LOAD and the knob should act on, only when it differs from the last thing
    /// sent. The App starts out with no pick, so that counts as already sent.</summary>
    private void SyncPick()
    {
        var path = IsOpen ? HighlightedPath : null;
        if (IsOpen == _sentActive && path == _sentPath) return;
        _sentActive = IsOpen;
        _sentPath = path;
        _sender.Send(new SetSearchPick(IsOpen, path, Ui("pick")));
    }

    private void NotifyHighlight()
    {
        Notify(nameof(HighlightedPath));
        Notify(nameof(ActionText));
        Notify(nameof(AlternateActionText));
    }

    private Origin Ui(string gesture) => new(InterfaceIds.MainUI, "glance", gesture);

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
