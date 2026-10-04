namespace Sholto.App.Library.Markers;

/// <summary>Memory-cue markers on tracks, as the view model sees them: positions only.</summary>
public interface IMarkerService
{
    /// <summary>Drop a marker at <paramref name="positionSec"/> seconds; returns its id.</summary>
    Task<int> AddAsync(Guid trackId, double positionSec);

    /// <summary>Marker positions in seconds for a track, earliest first.</summary>
    Task<IReadOnlyList<double>> ListPositionsAsync(Guid trackId);
}
