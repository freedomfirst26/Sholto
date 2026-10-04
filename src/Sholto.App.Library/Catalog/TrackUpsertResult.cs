namespace Sholto.App.Library.Catalog;

/// <summary>Outcome of <see cref="ITrackCatalog.UpsertAsync"/>.</summary>
/// <param name="PathToId">Path to track id for EVERY track in the catalog after the upsert,
/// not just the ones passed in (the scan uses it to stamp ids on rows).</param>
/// <param name="NewIds">Ids of tracks inserted by this call, in input order. Empty when none.</param>
public sealed record TrackUpsertResult(
    IReadOnlyDictionary<string, Guid> PathToId,
    IReadOnlyList<Guid> NewIds);
