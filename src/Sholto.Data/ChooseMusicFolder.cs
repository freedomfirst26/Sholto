namespace Sholto.Data;

/// <summary>The answer to <see cref="MusicFolderNeeded"/>: the folder the user picked, or null when they
/// cancelled the picker.</summary>
public readonly record struct ChooseMusicFolder(string? Path, Origin Origin) : ICommand;
