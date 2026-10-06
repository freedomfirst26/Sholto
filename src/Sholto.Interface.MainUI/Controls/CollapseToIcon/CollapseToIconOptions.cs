namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>What names and times one consumer of the collapse-to-icon sequence.</summary>
/// <param name="Key">Names the consumer (e.g. "faceplate"): for logs now, and for the saved hint count later.</param>
/// <param name="Timings">The phase lengths.</param>
public sealed record CollapseToIconOptions(string Key, CollapseToIconTimings Timings);
