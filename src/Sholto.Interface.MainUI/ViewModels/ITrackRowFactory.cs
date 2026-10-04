using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Builds a <see cref="TrackRow"/> for a library track summary.</summary>
public interface ITrackRowFactory
{
    TrackRow Create(TrackSummary summary);
}
