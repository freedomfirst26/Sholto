namespace Sholto.Data;

/// <summary>The user asked to pick a different music folder (the menu entry). The App answers with
/// <see cref="MusicFolderNeeded"/> and waits for <see cref="ChooseMusicFolder"/>.</summary>
public readonly record struct ChangeMusicFolder(Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
