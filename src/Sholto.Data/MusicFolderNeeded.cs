namespace Sholto.Data;

/// <summary>The App needs the user to choose a music folder. A fact: not replayed. An interface that can
/// ask shows a folder picker and answers with <see cref="ChooseMusicFolder"/> (a null path when
/// cancelled).</summary>
/// <param name="Reason">Why the App is asking, so the interface can word the prompt.</param>
/// <param name="MissingPath">The saved folder that could not be reached, for <see cref="MusicFolderReason.DriveNotFound"/>.</param>
public readonly record struct MusicFolderNeeded(MusicFolderReason Reason, string? MissingPath) : IEvent;
