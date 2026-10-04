namespace Sholto.Data;

/// <summary>The answer to <see cref="MusicFolderNeeded"/>: the folder the user picked, or null when they
/// cancelled the picker.</summary>
public readonly record struct ChooseMusicFolder(string? Path, Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
