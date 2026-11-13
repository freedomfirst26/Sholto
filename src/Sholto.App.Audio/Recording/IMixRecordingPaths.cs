namespace Sholto.App.Audio.Recording;

/// <summary>Chooses the file a new recording goes to. Runs on the app thread.</summary>
public interface IMixRecordingPaths
{
    /// <summary>A path that does not exist yet, named from <paramref name="localNow"/>, in a folder that does.</summary>
    string Next(DateTime localNow);
}
