using Sholto.App.Mixer;

namespace Sholto.App.Tests;

/// <summary>A mixer that holds the crossfader and nothing else, for handler tests that do not touch it.</summary>
public sealed class StubMixer : IMixer
{
    public double Crossfader { get; set; }

    public event Action? CrossfaderChanged { add { } remove { } }
}
