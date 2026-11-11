namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>The query text and the filter chips, the free words the rail is searched with, and the faint
/// <c>#tag</c> completion after the last word.</summary>
public interface IGlanceChipEditor
{
    /// <summary><see cref="Query"/> changed.</summary>
    event Action? QueryChanged;

    /// <summary><see cref="CompletionSuffix"/> changed.</summary>
    event Action? CompletionChanged;

    string Query { get; }

    IReadOnlyList<GlanceChip> Chips { get; }

    string? CompletionSuffix { get; }

    /// <summary>The free words of the query: what a crate or a tag is matched against. Operators such as
    /// <c>bpm:128</c>, <c>key:8A</c> and <c>#techno</c> are left out.</summary>
    string RailText(string query);

    /// <summary>Store the query; the caller has already checked it differs.</summary>
    void SetQuery(string query);

    /// <summary>Remove the chip a crate or tag rail item stands for if it is present, else add it and drop the
    /// query's free words. False on any other item.</summary>
    bool Toggle(object? railItem);

    /// <summary>Remove the last chip, when the query is empty and there is one.</summary>
    bool RemoveLast();

    /// <summary>Remove the chip that filters the same as <paramref name="chip"/>. False when there is none.</summary>
    bool Remove(GlanceChip chip);

    /// <summary>Turn the completed <c>#tag</c> word into a chip. False when nothing is completed.</summary>
    bool AcceptCompletion();

    void RefreshCompletion(bool databaseUp);
}
