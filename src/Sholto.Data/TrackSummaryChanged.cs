namespace Sholto.Data;

/// <summary>One library track's summary changed (BPM landed, tags edited, played, analysis progress...). A
/// fact, always published on the app thread. The rows of <see cref="LibraryRowsChanged"/> are a snapshot
/// from when they were published; this carries what changed since.</summary>
public readonly record struct TrackSummaryChanged(TrackSummary Summary) : IEvent;
