using Microsoft.Extensions.Options;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

public sealed class GlanceChipEditor : IGlanceChipEditor
{
    private readonly IQueryAsker _asker;
    private readonly IAppThread _appThread;
    private readonly ITagRecency _tagRecency;
    private readonly int _railLimit;

    private string _query = "";
    private IReadOnlyList<GlanceChip> _chips = [];
    private string? _completionToken;
    private string? _completionTag;
    private int _completionGen;

    public GlanceChipEditor(IQueryAsker asker, IAppThread appThread, ITagRecency tagRecency,
        IOptions<GlanceViewOptions> options)
    {
        _asker = asker;
        _appThread = appThread;
        _tagRecency = tagRecency;
        _railLimit = options.Value.TagRailLimit;
    }

    public event Action? QueryChanged;

    public event Action? CompletionChanged;

    public string Query => _query;

    public IReadOnlyList<GlanceChip> Chips => _chips;

    public string? CompletionSuffix { get; private set; }

    public string RailText(string query) => string.Join(' ', query
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        .Where(w => !IsOperatorWord(w)));

    public void SetQuery(string query)
    {
        _query = query;
        QueryChanged?.Invoke();
    }

    public bool Toggle(object? railItem)
    {
        var chip = railItem switch
        {
            GlanceRailCrate c => new GlanceChip(GlanceChipKind.Crate, c.Id, c.Name),
            GlanceRailTag t => new GlanceChip(GlanceChipKind.Tag, 0, t.Name),
            _ => null,
        };
        if (chip is null) return false;
        if (!Remove(chip))
        {
            if (chip.Kind == GlanceChipKind.Tag) _tagRecency.MarkUsed(chip.Name);
            _chips = [.. _chips, chip];
            ClearFreeWords();
        }
        return true;
    }

    public bool RemoveLast()
    {
        if (_query.Length > 0 || _chips.Count == 0) return false;
        _chips = [.. _chips.Take(_chips.Count - 1)];
        return true;
    }

    public bool Remove(GlanceChip chip)
    {
        var existing = _chips.FirstOrDefault(c => SameFilter(c, chip));
        if (existing is null) return false;
        _chips = [.. _chips.Where(c => !ReferenceEquals(c, existing))];
        return true;
    }

    public bool AcceptCompletion()
    {
        if (_completionTag is not { } tag || _completionToken is not { } token
            || TrailingTagToken(_query) != token) return false;
        _query = _query[..^token.Length].TrimEnd();
        QueryChanged?.Invoke();
        SetCompletion(null, null, null);
        _completionGen++;
        _tagRecency.MarkUsed(tag);
        _chips = [.. _chips, new GlanceChip(GlanceChipKind.Tag, 0, tag)];
        return true;
    }

    public void RefreshCompletion(bool databaseUp)
    {
        var gen = ++_completionGen;
        var token = TrailingTagToken(_query);
        if (token is null || !databaseUp)
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
            hits = await _asker.AskAsync<SearchTags, IReadOnlyList<TagHit>>(new SearchTags(fragment, _railLimit)) ?? [];
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

    private void SetCompletion(string? token, string? tag, string? suffix)
    {
        _completionToken = token;
        _completionTag = tag;
        if (CompletionSuffix == suffix) return;
        CompletionSuffix = suffix;
        CompletionChanged?.Invoke();
    }

    /// <summary>The query without its plain words; <c>bpm:</c>, <c>key:</c> and <c>#</c> tokens stay.</summary>
    private void ClearFreeWords()
    {
        var kept = string.Join(' ', _query
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(IsOperatorWord));
        if (kept == _query) return;
        _query = kept;
        QueryChanged?.Invoke();
    }

    /// <summary>A <c>#</c>, <c>bpm:</c> or <c>key:</c> word: an operator, not a plain word.</summary>
    private bool IsOperatorWord(string word) => word.StartsWith('#')
        || word.StartsWith("bpm:", StringComparison.OrdinalIgnoreCase)
        || word.StartsWith("key:", StringComparison.OrdinalIgnoreCase);

    /// <summary>The last word of the query when it is a <c>#</c> fragment of at least two characters and the
    /// query does not end in whitespace; otherwise null.</summary>
    private string? TrailingTagToken(string query)
    {
        if (query.Length == 0 || char.IsWhiteSpace(query[^1])) return null;
        var word = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[^1];
        return word.StartsWith('#') && word.Length >= 2 ? word : null;
    }

    private bool SameFilter(GlanceChip a, GlanceChip b) => a.Kind == b.Kind
        && (a.Kind == GlanceChipKind.Crate
            ? a.CrateId == b.CrateId
            : string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
}
