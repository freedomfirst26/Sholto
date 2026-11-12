namespace Sholto.Interface.MainUI.Tests;

/// <summary>
/// The MainViewModel test classes were once one xUnit class, so their tests ran one at a time.
/// Joining them to one collection keeps that.
/// </summary>
[CollectionDefinition(Name)]
public sealed class MainViewModelCollection
{
    public const string Name = "MainViewModel";
}
