namespace Sholto.Data;

/// <summary>Why the App needs the user to look at the folders (music and analysis).</summary>
public enum FoldersReason
{
    /// <summary>No music folder has been chosen yet (first run).</summary>
    FirstRun,

    /// <summary>An existing install with a music folder but no analysis folder saved: asked once on upgrade.</summary>
    Upgrade,

    /// <summary>A saved folder cannot be reached (drive unmounted or remounted under another name).</summary>
    DriveNotFound,

    /// <summary>The user asked to change the folders.</summary>
    Change,
}
