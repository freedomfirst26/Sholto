namespace Sholto.Data;

/// <summary>Crates whose name matches the query (all of them for an empty query). Empty without a database.</summary>
public readonly record struct SearchCrates(string Query) : IQuery<Task<IReadOnlyList<CrateRef>>>;
