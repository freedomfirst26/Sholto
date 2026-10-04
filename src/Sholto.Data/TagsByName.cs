namespace Sholto.Data;

/// <summary>The tags of the given names that exist, with their track counts. Empty without a database.</summary>
public readonly record struct TagsByName(IReadOnlyCollection<string> Names) : IQuery<Task<IReadOnlyList<TagHit>>>;
