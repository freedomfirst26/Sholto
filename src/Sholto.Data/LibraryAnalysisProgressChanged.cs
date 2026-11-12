namespace Sholto.Data;

/// <summary>Where the library analysis has got to. Fact: sent once when it starts, after every song that ends,
/// and once more with <paramref name="Running"/> false when it finishes or is cancelled.</summary>
/// <param name="Done">Songs analysed now or found already analysed.</param>
/// <param name="Total">Songs in the run.</param>
/// <param name="Failed">Songs whose analysis failed (or was cut short by a cancel).</param>
/// <param name="Running">False on the last event of a run.</param>
public readonly record struct LibraryAnalysisProgressChanged(int Done, int Total, int Failed, bool Running) : IEvent;
