using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.Tests.CollapseToIcon;

/// <summary>The environment variable wins over the desktop setting; an unreadable setting means motion stays on.</summary>
public class DesktopMotionPreferenceTests
{
    private sealed class FakeSetting(string? value) : IDesktopAnimationSetting
    {
        public string? Read() => value;
    }

    private static bool Reduced(string? environment, string? setting) =>
        new DesktopMotionPreference(_ => environment, new FakeSetting(setting)).Reduced;

    [Theory]
    [InlineData(null, "true", false)]
    [InlineData(null, "false", true)]
    [InlineData(null, null, false)]
    [InlineData(null, "garbage", false)]
    [InlineData("1", "true", true)]
    [InlineData("true", "true", true)]
    [InlineData("0", "false", false)]
    [InlineData("false", "false", false)]
    [InlineData("", "false", true)]
    public void Reduced_follows_the_variable_then_the_desktop_then_stays_off(string? environment, string? setting, bool expected) =>
        Assert.Equal(expected, Reduced(environment, setting));

    [Fact]
    public void A_missing_gsettings_tool_reads_as_null_not_an_error()
    {
        var original = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", "/nonexistent");
            Assert.Null(new GsettingsAnimationSetting().Read());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", original);
        }
    }
}
