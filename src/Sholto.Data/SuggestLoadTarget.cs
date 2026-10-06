namespace Sholto.Data;

/// <summary>Which deck a new track should load into by default (0 or 1), by the time-left rule.</summary>
public readonly record struct SuggestLoadTarget : IQuery<int>;
