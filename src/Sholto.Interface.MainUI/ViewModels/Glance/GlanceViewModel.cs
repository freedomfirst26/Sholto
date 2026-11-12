using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Port of the mockup's Glance overlay onto the bus. Ranking is the App's (<see cref="RankTracks"/>);
/// this class keeps the target deck, the zone and the highlight, tells the App which track the highlight
/// points at (<see cref="SetSearchPick"/>) so the controller's LOAD and knob follow it, and shows the answers.
/// The query and chips, the ranking requests, the rail's content and the Track List state are its
/// collaborators', handed in through the constructor.</summary>
public sealed class GlanceViewModel :
    IGlanceViewModel,
    IFrameTickHandler,
    IEventHandler<LoadConfirmPending>,
    IEventHandler<LoadAccepted>,
    IEventHandler<LibraryRowsChanged>,
    IEventHandler<TrackSummaryChanged>,
    IEventHandler<TrackListChanged>,
    IEventHandler<LibraryDatabaseAttached>,
    IEventHandler<DeckContentChanged>,
    IEventHandler<DeckTempoChanged>
{
    /// <summary>Frame order for the re-rank tick.</summary>
    public const int ClockOrder = 100;

    private readonly ICommandSender _sender;
    private readonly IQueryAsker _asker;
    private readonly IGlanceRowSource _rowSource;
    private readonly IMotionPreference _motion;
    private readonly IGlanceChipEditor _editor;
    private readonly IGlanceRankPipeline _pipeline;
    private readonly IGlanceRail _rail;
    private readonly IGlanceTrackList _trackList;

    private bool _databaseUp;
    private int _libraryCount;
    private bool _resetHighlight;

    private bool _sentActive;
    private string? _sentPath;

    public GlanceViewModel(
        ICommandSender sender, IQueryAsker asker, IEventSubscriber subscriber, IFrameClock clock,
        IGlanceRowSource rowSource, IGlanceHeaderViewModel header, IMotionPreference motion,
        IGlanceChipEditor editor, IGlanceRankPipeline pipeline, IGlanceRail rail, IGlanceTrackList trackList)
    {
        _sender = sender;
        _asker = asker;
        _rowSource = rowSource;
        _motion = motion;
        _editor = editor;
        _pipeline = pipeline;
        _rail = rail;
        _trackList = trackList;
        Header = header;
        _editor.QueryChanged += () => Notify(nameof(Query));
        _editor.CompletionChanged += () => Notify(nameof(CompletionSuffix));
        _pipeline.ReassessingChanged += () => Notify(nameof(IsReassessing));
        _pipeline.Shown += OnShown;
        _rail.Changed += RebuildRail;
        _trackList.ClearArmedChanged += () => Notify(nameof(ClearArmedText));
        clock.Subscribe(this, ClockOrder);
        subscriber.Subscribe<LoadConfirmPending>(this);
        subscriber.Subscribe<LoadAccepted>(this);
        subscriber.Subscribe<LibraryRowsChanged>(this);
        subscriber.Subscribe<TrackSummaryChanged>(this);
        subscriber.Subscribe<TrackListChanged>(this);
        subscriber.Subscribe<LibraryDatabaseAttached>(this);
        subscriber.Subscribe<DeckContentChanged>(this);
        subscriber.Subscribe<DeckTempoChanged>(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? SelectAllOnOpen;

    public event Action? ResultsReplaced;

    public IGlanceHeaderViewModel Header { get; }

    public bool IsOpen { get; private set; }

    public string Query
    {
        get => _editor.Query;
        set
        {
            if (_editor.Query == value) return;
            var railChanged = _editor.RailText(_editor.Query) != _editor.RailText(value);
            _editor.SetQuery(value);
            if (!IsOpen) return;
            _resetHighlight = true;
            TableIndex = 0;
            Rank(personInitiated: true);
            if (railChanged) RefreshRail();
            _editor.RefreshCompletion(_databaseUp);
        }
    }

    public string? CompletionSuffix => _editor.CompletionSuffix;

    public int Target { get; private set; }

    public IReadOnlyList<GlanceRow> Rows { get; private set; } = [];

    public int ScopeCount => _pipeline.ScopeChipsActive || _pipeline.ResultScopeCount > 0
        ? _pipeline.ResultScopeCount
        : _libraryCount;

    public IReadOnlyList<GlanceChip> Chips => _editor.Chips;

    public bool IsReassessing => _pipeline.IsReassessing;

    public bool AnimateResults => !_motion.Reduced;

    public string? ScopeEmptyText => _pipeline.ScopeEmptyText;

    public IReadOnlyList<string> FilterChips { get; private set; } = [];

    public GlanceZone Zone { get; private set; }

    public int TableIndex { get; private set; }

    public IReadOnlyList<object> RailItems { get; private set; } = [];

    public int RailIndex { get; private set; }

    public string? HighlightedPath => Zone == GlanceZone.Table
        ? TableIndex >= 0 && TableIndex < Rows.Count ? Rows[TableIndex].FilePath : null
        : null;

    /// <summary>The rail item at <see cref="RailIndex"/>, in either zone.</summary>
    private object? RailItem => RailIndex >= 0 && RailIndex < RailItems.Count ? RailItems[RailIndex] : null;

    private object? HighlightedRailItem => Zone == GlanceZone.Rail ? RailItem : null;

    public string ActionText => Zone == GlanceZone.Table
        ? HighlightedPath is null ? "" : $"Load to Deck {Target + 1}"
        : RailItem switch
        {
            GlanceRailCrate { IsActive: true } or GlanceRailTag { IsActive: true } => "Remove filter",
            GlanceRailCrate or GlanceRailTag => "Add filter",
            _ => "",
        };

    public event Action<string>? AddedToTrackList;

    public int TrackListCount => _trackList.Count;

    public GlanceLoadState Deck1LoadState => DeckLoadState();

    public GlanceLoadState Deck2LoadState => DeckLoadState();

    public GlanceLoadState TrackListLoadState => _trackList.LoadState(HighlightedPath, HighlightedRailItem);

    public int TrackListLoadCount => _trackList.LoadCount(HighlightedPath, HighlightedRailItem);

    public string? ClearArmedText => _trackList.ClearArmedText;

    private GlanceLoadState DeckLoadState() => HighlightedPath is null ? GlanceLoadState.Off : GlanceLoadState.Ready;

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
        RailIndex = _rail.FirstSelectable(RailItems);
        SelectAllOnOpen?.Invoke();
        Rank(personInitiated: true);
        RefreshRail();
        NotifyHighlight();
        SyncPick();
    }

    public void Close()
    {
        if (!IsOpen) return;
        _trackList.Disarm();
        IsOpen = false;
        Notify(nameof(IsOpen));
        _pipeline.Close();
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
        if (Zone == GlanceZone.Rail && RailItem is GlanceRailCrate or GlanceRailTag)
        {
            ActivateRailItem();
            return;
        }
        LoadHighlighted(Target);
    }

    public void ActivateRailItem()
    {
        if (!IsOpen || !_editor.Toggle(RailItem)) return;
        ChipsChanged();
    }

    public void LoadToTrackList()
    {
        if (!IsOpen) return;
        _trackList.Load(HighlightedPath, HighlightedRailItem);
    }

    public void ToggleTrackListLoad()
    {
        if (!IsOpen) return;
        if (TrackListLoadState == GlanceLoadState.InList) _trackList.Unload(HighlightedPath, HighlightedRailItem);
        else LoadToTrackList();
    }

    public void ClearTrackList()
    {
        if (!IsOpen) return;
        _trackList.Clear();
    }

    public bool AcceptTagCompletion()
    {
        if (!IsOpen || !_editor.AcceptCompletion()) return false;
        ChipsChanged();
        return true;
    }

    public bool RemoveLastChip()
    {
        if (!_editor.RemoveLast()) return false;
        ChipsChanged();
        return true;
    }

    public void RemoveChip(GlanceChip chip)
    {
        if (_editor.Remove(chip)) ChipsChanged();
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
        _editor.RefreshCompletion(_databaseUp);
        NotifyHighlight();
    }

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

    public void ToggleHighlightedInTrackList()
    {
        if (!IsOpen || HighlightedPath is not { } path) return;
        _trackList.Toggle(path);
    }

    // ---- Events --------------------------------------------------------------------------------

    public void Handle(in LoadConfirmPending e)
    {
        if (e.Pending) ChangeTarget(e.Deck, resetHighlight: false);
    }

    public void Handle(in LoadAccepted e) => Close();

    public void Handle(in LibraryRowsChanged e)
    {
        _libraryCount = e.Rows.Count;
        Notify(nameof(ScopeCount));
        _pipeline.MarkDirty();
    }

    public void Handle(in TrackSummaryChanged e) => _pipeline.MarkDirty();

    public void Handle(in DeckContentChanged e) => _pipeline.MarkDirty();

    public void Handle(in DeckTempoChanged e) => _pipeline.MarkDirty();

    public void Handle(in TrackListChanged e)
    {
        _trackList.Apply(e);
        foreach (var row in Rows)
        {
            var wasIn = row.IsInTrackList;
            row.IsInTrackList = _trackList.Contains(row.FilePath);
            if (IsOpen && !wasIn && row.IsInTrackList) AddedToTrackList?.Invoke(row.FilePath);
        }
        NotifyLoadStates();
        Notify(nameof(TrackListCount));
        _trackList.DisarmIfEmpty();
        RebuildRail();
    }

    public void Handle(in LibraryDatabaseAttached e)
    {
        _databaseUp = e.Available;
        if (_databaseUp && IsOpen) RefreshRail();
    }

    public void OnFrame(DateTime now)
    {
        _trackList.Tick(now);
        if (!IsOpen) return;
        if (_pipeline.Tick(now)) Rank(personInitiated: false);
    }

    // ---- Ranking -------------------------------------------------------------------------------

    private void Rank(bool personInitiated) =>
        _pipeline.Rank(_editor.Query, Target, _editor.Chips, personInitiated);

    private void OnShown(RankTracks request, RankedTracks ranked)
    {
        var previous = _resetHighlight ? null : HighlightedTablePath();
        var rows = new List<GlanceRow>(ranked.Rows.Count);
        foreach (var r in ranked.Rows)
            rows.Add(new GlanceRow(_rowSource.RowFor(r.Summary), r, _trackList.Contains(r.Summary.FilePath)));
        var index = 0;
        if (previous is not null)
        {
            var found = rows.FindIndex(r => r.FilePath == previous);
            if (found >= 0) index = found;
        }
        _resetHighlight = false;

        Rows = rows;
        FilterChips = ranked.FilterChips;
        TableIndex = index;
        Header.Apply(ranked, request.TargetDeck);
        Notify(nameof(Rows));
        Notify(nameof(FilterChips));
        Notify(nameof(TableIndex));
        Notify(nameof(ScopeCount));
        Notify(nameof(ScopeEmptyText));
        RebuildRail();
        NotifyHighlight();
        SyncPick();
        ResultsReplaced?.Invoke();
    }

    private string? HighlightedTablePath() =>
        TableIndex >= 0 && TableIndex < Rows.Count ? Rows[TableIndex].FilePath : null;

    // ---- Rail ----------------------------------------------------------------------------------

    private void RefreshRail() => _rail.Refresh(_editor.RailText(_editor.Query), _databaseUp);

    private void RebuildRail()
    {
        var items = _rail.Items(_editor.Chips, _pipeline.CrateCounts, _pipeline.TagCounts, _trackList.Keys);
        RailIndex = _rail.IndexAfterRebuild(RailItems, RailIndex, items);
        RailItems = items;
        Notify(nameof(RailItems));
        Notify(nameof(RailIndex));
        NotifyHighlight();
        SyncPick();
    }

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
        NotifyLoadStates();
    }

    private void NotifyLoadStates()
    {
        Notify(nameof(Deck1LoadState));
        Notify(nameof(Deck2LoadState));
        Notify(nameof(TrackListLoadState));
        Notify(nameof(TrackListLoadCount));
    }

    private Origin Ui(string gesture) => new(InterfaceIds.MainUI, "glance", gesture);

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
