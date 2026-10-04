namespace Sholto.Data;

/// <summary>Tag names starting with <see cref="Prefix"/>, for autocomplete. Empty without a database.</summary>
public readonly record struct SuggestTags(string Prefix, int Limit) : IQuery<Task<IReadOnlyList<string>>>;
