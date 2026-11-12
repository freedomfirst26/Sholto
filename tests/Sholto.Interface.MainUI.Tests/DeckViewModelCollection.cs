namespace Sholto.Interface.MainUI.Tests;

/// <summary>
/// The DeckViewModel test classes were once one xUnit class, so their tests ran one at a time.
/// Joining them to one collection keeps that.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DeckViewModelCollection
{
    public const string Name = "DeckViewModel";
}
