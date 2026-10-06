using Sholto.App.Analysis.Analyzers.Beats;

namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>Maps a bar-positioned <see cref="SongSection"/> onto time. An extension
/// rather than an injected mapper: it is a pure one-line function of the section and
/// the grid, has no state or collaborators to substitute, and extension methods are the
/// permitted static form.</summary>
public static class SongSectionExtensions
{
    public static double StartSec(this SongSection section, Beatgrid grid) => grid.DownbeatAt(section.StartBar);

    public static double EndSec(this SongSection section, Beatgrid grid) => grid.DownbeatAt(section.EndBar);
}
