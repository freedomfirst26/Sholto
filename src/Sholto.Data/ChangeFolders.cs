namespace Sholto.Data;

/// <summary>The user asked to change the folders (the menu entry or the unreachable-library banner). The App
/// answers with <see cref="FoldersNeeded"/> and waits for <see cref="ChooseFolders"/>.</summary>
public readonly record struct ChangeFolders(Origin Origin) : ICommand;
