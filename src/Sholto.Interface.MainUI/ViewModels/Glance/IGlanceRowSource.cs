using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Finds the library row for a file, so Glance rows reuse the row the library list shows.</summary>
public interface IGlanceRowSource
{
    /// <summary>The visible row for <paramref name="path"/>, or null when it is not shown.</summary>
    TrackRow? RowFor(string path);

    /// <summary>The row for a ranked track: the visible row when the library shows it, otherwise a row built
    /// for it. Glance ranks the whole catalogue, so a ranked track need not be in the visible library.</summary>
    TrackRow RowFor(TrackSummary summary);
}
