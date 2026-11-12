using Sholto.App.Decks;

namespace Sholto.App.Tests;

/// <summary>The BEAT JUMP size options keep their documented defaults.</summary>
public class BeatJumpOptionsTests
{
    [Fact]
    public void Defaults_are_pinned()
    {
        var options = new BeatJumpOptions();

        Assert.Equal(0.125, options.MinScale);
        Assert.Equal(8.0, options.MaxScale);
    }
}
