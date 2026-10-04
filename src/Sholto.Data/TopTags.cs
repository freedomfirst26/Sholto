namespace Sholto.Data;

/// <summary>The most-used tags. Empty without a database.</summary>
public readonly record struct TopTags(int Limit) : IQuery<Task<IReadOnlyList<TagHit>>>;
