using Sholto.Data;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

public sealed class TrackRowFactory(IThemeContext theme) : ITrackRowFactory
{
    private readonly IThemeContext _theme = theme;

    public TrackRow Create(TrackSummary summary) => new(summary, _theme);
}
