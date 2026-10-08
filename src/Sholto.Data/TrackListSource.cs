namespace Sholto.Data;

/// <summary>One source that brought songs into the Track List.</summary>
/// <param name="Key">Identity: <c>crate:{id}</c>, <c>tag:{lowercased}</c>, or <c>songs</c>.</param>
/// <param name="Kind">The kind of source.</param>
/// <param name="Name">Display name.</param>
/// <param name="Count">Songs this source brought.</param>
public sealed record TrackListSource(string Key, TrackListSourceKind Kind, string Name, int Count);
