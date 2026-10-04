using Sholto.App.Audio;

namespace Sholto.App.Tests;

/// <summary>Recognises the one device name it was given as the controller's own card.</summary>
internal sealed class FakeControllerSoundCard(string name) : IControllerSoundCard
{
    private readonly string _name = name;

    public bool Matches(string? nodeOrDesc) => nodeOrDesc == _name;
}
