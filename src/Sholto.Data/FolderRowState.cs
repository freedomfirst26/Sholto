namespace Sholto.Data;

/// <summary>Where one folder row stands when the App asks about the folders.</summary>
public enum FolderRowState
{
    /// <summary>A saved folder that exists.</summary>
    Ok,

    /// <summary>A saved folder that cannot be reached (drive unmounted or remounted under another name).</summary>
    Missing,

    /// <summary>No folder has been chosen yet; the row may carry a suggestion.</summary>
    NotChosen,

    /// <summary>Set by an environment variable: shown, not changeable and not saved.</summary>
    Fixed,
}
