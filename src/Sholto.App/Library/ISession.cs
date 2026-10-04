namespace Sholto.App.Library;

/// <summary>Which tracks have been loaded into a deck during this run of the app.</summary>
public interface ISession
{
    /// <summary>Fires once per first time a track is loaded into a deck.</summary>
    event Action<string>? TrackPlayed;

    /// <summary>Mark <paramref name="filePath"/> as played. Idempotent.</summary>
    void MarkPlayed(string filePath);

    /// <summary>Has <paramref name="filePath"/> been loaded into a deck this run?</summary>
    bool HasPlayed(string filePath);
}
