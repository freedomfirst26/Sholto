using System.ComponentModel;
using System.Globalization;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>One row of the Glance table: the library <see cref="TrackRow"/> plus what the App's ranking said
/// about it. Holds levels, not brushes; the XAML maps a level to a theme resource through a class.</summary>
public sealed class GlanceRow(TrackRow row, RankedTrack ranked, bool isInTrackList = false) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isInTrackList = isInTrackList;

    public TrackRow Row { get; } = row;

    public string FilePath => Row.FilePath;

    public bool IsReference { get; } = ranked.IsReference;

    /// <summary>The fit bar. The reference deck's own track shows none.</summary>
    public FitLevel FitLevel { get; } = ranked.IsReference ? FitLevel.None : ranked.Fit;

    /// <summary>Tempo difference to the reference, "+0.8%"; empty when there is none or this is the reference.</summary>
    public string TempoDeltaDisplay { get; } = ranked is { IsReference: false, TempoDeltaPercent: { } d }
        ? d.ToString("+0.0;-0.0;+0.0", CultureInfo.InvariantCulture) + "%"
        : "";

    /// <summary>At most 2 % is good, at most 4 % usable, anything more a clash; None when there is no delta.</summary>
    public FitLevel TempoDeltaLevel { get; } = ranked is { IsReference: false, TempoDeltaPercent: { } d }
        ? Math.Abs(d) <= 2 ? FitLevel.Good : Math.Abs(d) <= 4 ? FitLevel.Usable : FitLevel.Clash
        : FitLevel.None;

    /// <summary>Played tracks and the reference recede.</summary>
    public bool IsFaded => IsReference || Row.IsPlayed;

    /// <summary>The song is in the Track List (the library rows are the list): the row's star is filled.</summary>
    public bool IsInTrackList
    {
        get => _isInTrackList;
        set
        {
            if (_isInTrackList == value) return;
            _isInTrackList = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInTrackList)));
        }
    }

    /// <summary>"Artist", then " · playing now" or " · played", then " · analysing" or " · analysis failed".</summary>
    public string Subtitle
    {
        get
        {
            var text = Row.Artist;
            if (IsReference) text += " · playing now";
            else if (Row.IsPlayed) text += " · played";
            if (Row.IsAnalyzing) text += " · analysing";
            else if (Row.ShowAnalysisFailed) text += " · analysis failed";
            return text;
        }
    }
}
