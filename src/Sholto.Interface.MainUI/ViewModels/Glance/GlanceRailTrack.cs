using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>A track in the rail (shortlist or recent loads). <see cref="Row"/> is the library row when the
/// track is among the visible rows, which carries the played-faded key chip; null when the active filter
/// hides it.</summary>
public sealed record GlanceRailTrack(TrackSummary Summary, TrackRow? Row)
{
    public string FilePath => Summary.FilePath;

    public string Title => Summary.Title;
}
