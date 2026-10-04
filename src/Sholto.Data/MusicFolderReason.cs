namespace Sholto.Data;

/// <summary>Why the App needs a music folder from the user.</summary>
public enum MusicFolderReason
{
    /// <summary>No folder has been chosen yet (first run).</summary>
    FirstRun,

    /// <summary>The saved folder cannot be reached (drive unmounted or remounted under another name).</summary>
    DriveNotFound,

    /// <summary>The user asked to change the folder.</summary>
    Change,
}
