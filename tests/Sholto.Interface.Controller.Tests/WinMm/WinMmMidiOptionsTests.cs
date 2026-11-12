using Sholto.Interface.Controller.WinMm;

namespace Sholto.Interface.Controller.Tests.WinMm;

/// <summary>Pins the WinMM MIDI option defaults.</summary>
public sealed class WinMmMidiOptionsTests
{
    [Fact]
    public void The_device_list_is_polled_every_second_by_default() =>
        Assert.Equal(TimeSpan.FromSeconds(1), new WinMmMidiOptions().PollInterval);
}
