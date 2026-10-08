namespace Sholto.App.Tests;

/// <summary>The load windows keep the values they had before they became options.</summary>
public class LoadOptionsTests
{
    [Fact]
    public void Defaults_equal_the_former_constants()
    {
        var options = new LoadOptions();

        Assert.Equal(10, options.UndoWindowSeconds);
        Assert.Equal(3, options.ConfirmWindowSeconds);
    }
}
