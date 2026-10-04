namespace Sholto.Data;

/// <summary>The user asked to pick a different output device (the menu entry). The App answers with
/// <see cref="OutputDeviceNeeded"/> and waits for <see cref="ChooseOutputDevice"/>.</summary>
public readonly record struct ChangeOutputDevice(Origin Origin) : ICommand
{
    /// <summary>Not a deck command.</summary>
    public int Deck => -1;
}
