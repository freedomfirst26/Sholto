namespace Sholto.Data;

/// <summary>Tags matching a search query. Empty without a database.</summary>
public readonly record struct SearchTags(string Query, int Limit) : IQuery<Task<IReadOnlyList<TagHit>>>;
