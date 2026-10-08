using Sholto.Data;

namespace Sholto.App.Lifecycle;

/// <summary>One saved Track List source: its key, kind and display name.</summary>
public sealed record SavedTrackListSource(string Key, TrackListSourceKind Kind, string Name);
