using Microsoft.Extensions.Options;
using Sholto.Interface.Controller.Tests.WinMm;
using Sholto.Interface.Controller.WinMm;

namespace Sholto.Interface.Controller.Tests;

/// <summary>The opener factory gives WinMM on Windows and raw ALSA elsewhere.</summary>
public sealed class MidiPortOpenerFactoryTests
{
    private readonly MidiPortOpenerFactory _factory =
        new(Options.Create(new WinMmMidiOptions()), new ManualTimeProvider(), new RecordingLogFactory());

    [Fact]
    public void Windows_gives_the_winmm_opener() => Assert.IsType<WinMmMidiPortOpener>(_factory.Windows());

    [Fact]
    public void Linux_gives_the_alsa_opener() => Assert.IsType<AlsaRawMidiOpener>(_factory.Linux());

    [Fact]
    public void Create_picks_by_the_running_os()
    {
        var opener = _factory.Create();

        if (OperatingSystem.IsWindows()) Assert.IsType<WinMmMidiPortOpener>(opener);
        else Assert.IsType<AlsaRawMidiOpener>(opener);
    }
}
