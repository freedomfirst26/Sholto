namespace Sholto.App.Audio.Tests;

public class VarispeedOptionsTests
{
    [Fact]
    public void Default_top_speed_is_8x()
    {
        var options = new VarispeedOptions();
        Assert.Equal(8.0, options.MaxSpeed);
    }
}
