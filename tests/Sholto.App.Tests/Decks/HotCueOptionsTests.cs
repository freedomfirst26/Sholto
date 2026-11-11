using Sholto.App.Decks;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>The hot-cue options keep their documented defaults.</summary>
public class HotCueOptionsTests
{
    [Fact]
    public void Defaults_are_pinned()
    {
        var options = new HotCueOptions();

        Assert.Equal(CueColour.Green, options.DefaultColour);
        Assert.True(options.SnapToBeat);
    }
}
