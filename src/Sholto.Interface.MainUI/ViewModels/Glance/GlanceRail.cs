using Microsoft.Extensions.Options;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

public sealed class GlanceRail : IGlanceRail
{
    private readonly IQueryAsker _asker;
    private readonly IAppThread _appThread;
    private readonly ITagRecency _tagRecency;
    private readonly IGlanceRailItemsFactory _itemsFactory;
    private readonly int _railLimit;
    private readonly ILog _log;

    private IReadOnlyList<CrateRef> _crates = [];
    private IReadOnlyList<TagHit> _tags = [];
    private int _railGen;

    public GlanceRail(IQueryAsker asker, IAppThread appThread, ITagRecency tagRecency,
        IGlanceRailItemsFactory itemsFactory, IOptions<GlanceViewOptions> options, ILogFactory logs)
    {
        _asker = asker;
        _appThread = appThread;
        _tagRecency = tagRecency;
        _itemsFactory = itemsFactory;
        _railLimit = options.Value.TagRailLimit;
        _log = logs.Create("Glance");
    }

    public event Action? Changed;

    public void Refresh(string railText, bool databaseUp)
    {
        var gen = ++_railGen;
        if (!databaseUp)
        {
            _crates = [];
            _tags = [];
            Changed?.Invoke();
            return;
        }
        _ = RefreshAsync(gen, railText);
    }

    private async Task RefreshAsync(int gen, string text)
    {
        IReadOnlyList<CrateRef> crates = [];
        IReadOnlyList<TagHit> tags = [];
        try
        {
            crates = await _asker.AskAsync<SearchCrates, IReadOnlyList<CrateRef>>(new SearchCrates(text)) ?? [];
            if (text.Length == 0)
            {
                // Tags picked earlier this session lead, newest first; the most-used tags fill the rest.
                var recentNames = _tagRecency.RecentNames(_railLimit);
                var recentHits = await _asker.AskAsync<TagsByName, IReadOnlyList<TagHit>>(new TagsByName(recentNames)) ?? [];
                var byName = recentHits.ToDictionary(h => h.Name, StringComparer.OrdinalIgnoreCase);
                var ordered = recentNames.Where(byName.ContainsKey).Select(n => byName[n]);
                var top = await _asker.AskAsync<TopTags, IReadOnlyList<TagHit>>(new TopTags(_railLimit)) ?? [];
                tags = [.. ordered.Concat(top).DistinctBy(h => h.Name, StringComparer.OrdinalIgnoreCase).Take(_railLimit)];
            }
            else
            {
                var matches = await _asker.AskAsync<SearchTags, IReadOnlyList<TagHit>>(new SearchTags(text, _railLimit)) ?? [];
                tags = _tagRecency.OrderRecentFirst(matches, h => h.Name);
            }
        }
        catch (Exception ex) { _log.Error($"rail search failed: {ex.Message}"); }
        _appThread.Post(() =>
        {
            if (gen != _railGen) return;
            _crates = crates;
            _tags = tags;
            Changed?.Invoke();
        });
    }

    public IReadOnlyList<object> Items(IReadOnlyList<GlanceChip> chips, IReadOnlyDictionary<int, int>? crateCounts,
        IReadOnlyDictionary<string, int>? tagCounts, IReadOnlySet<string> trackListKeys) =>
        _itemsFactory.Create(_crates, _tags, chips, crateCounts, tagCounts, trackListKeys);

    public int IndexAfterRebuild(IReadOnlyList<object> oldItems, int oldIndex, IReadOnlyList<object> newItems)
    {
        var selected = oldIndex >= 0 && oldIndex < oldItems.Count ? RailKey(oldItems[oldIndex]) : null;
        var index = selected is null ? -1 : newItems.ToList().FindIndex(i => RailKey(i) == selected);
        return index >= 0 ? index : FirstSelectable(newItems);
    }

    public int FirstSelectable(IReadOnlyList<object> items)
    {
        for (var i = 0; i < items.Count; i++)
            if (items[i] is not GlanceRailHeader) return i;
        return 0;
    }

    private string? RailKey(object item) => item switch
    {
        GlanceRailCrate c => "c:" + c.Id,
        GlanceRailTag g => "g:" + g.Name,
        _ => null,
    };
}
