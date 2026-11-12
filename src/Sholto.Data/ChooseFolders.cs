namespace Sholto.Data;

/// <summary>The answer to <see cref="FoldersNeeded"/>: the folders the user settled on, or both null when
/// they cancelled. A path equal to the current one means "keep it".</summary>
public readonly record struct ChooseFolders(string? Music, string? Analysis, Origin Origin) : ICommand;
