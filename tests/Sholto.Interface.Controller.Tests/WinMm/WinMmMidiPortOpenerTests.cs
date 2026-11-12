using Microsoft.Extensions.Options;
using NAudio;
using Sholto.Interface.Controller.WinMm;

namespace Sholto.Interface.Controller.Tests.WinMm;

/// <summary>The WinMM opener finds the controller by name on fake devices, never throws, and says when another
/// app holds the controller.</summary>
public sealed class WinMmMidiPortOpenerTests
{
    private readonly FakeWinMmDevices _devices = new();
    private readonly RecordingLogFactory _logs = new();
    private readonly WinMmMidiPortOpener _opener;

    public WinMmMidiPortOpenerTests() =>
        _opener = new WinMmMidiPortOpener(_devices, Options.Create(new WinMmMidiOptions()), new ManualTimeProvider(), _logs);

    [Fact]
    public void Opens_the_first_input_and_output_whose_names_contain_the_device_name_ignoring_case()
    {
        _devices.Inputs.AddRange(["Keyboard", "ddj-flx4", "DDJ-FLX4 #2"]);
        _devices.Outputs.AddRange(["Microsoft GS Wavetable Synth", "DDJ-FLX4"]);

        var port = _opener.Open("DDJ-FLX4");

        Assert.IsType<WinMmMidiPort>(port);
        Assert.Equal(1, Assert.Single(_devices.OpenedIns).Index);
        Assert.Equal(1, Assert.Single(_devices.OpenedOuts).Index);
        Assert.True(_devices.OpenedIns[0].Port.Started);
    }

    [Fact]
    public void Gives_null_when_no_input_matches()
    {
        _devices.Outputs.Add("DDJ-FLX4");

        Assert.Null(_opener.Open("DDJ-FLX4"));
        Assert.Empty(_devices.OpenedOuts);
    }

    [Fact]
    public void Gives_null_when_no_output_matches()
    {
        _devices.Inputs.Add("DDJ-FLX4");

        Assert.Null(_opener.Open("DDJ-FLX4"));
        Assert.Empty(_devices.OpenedIns);
    }

    [Fact]
    public void Gives_null_and_logs_when_the_device_list_cannot_be_read()
    {
        _devices.ListFails = new InvalidOperationException("winmm gone");

        Assert.Null(_opener.Open("DDJ-FLX4"));
        Assert.Contains(_logs.Lines, l => l.Message.Contains("winmm gone"));
    }

    [Fact]
    public void A_failed_output_open_gives_null_and_closes_the_input_it_had_opened()
    {
        Plug();
        _devices.OutFails[0] = new MmException(MmResult.BadDeviceId, "midiOutOpen");

        Assert.Null(_opener.Open("DDJ-FLX4"));
        Assert.Equal(1, _devices.OpenedIns[0].Port.DisposeCount);
    }

    [Fact]
    public void A_failed_input_start_gives_null_and_closes_both_sides()
    {
        Plug();
        _devices.StartFails = new MmException(MmResult.UnspecifiedError, "midiInStart");

        Assert.Null(_opener.Open("DDJ-FLX4"));
        Assert.Equal(1, _devices.OpenedIns[0].Port.DisposeCount);
        Assert.Equal(1, _devices.OpenedOuts[0].Port.DisposeCount);
    }

    [Fact]
    public void An_already_allocated_input_logs_in_use_by_another_app_with_the_device_name()
    {
        Plug();
        _devices.InFails[0] = new MmException(MmResult.AlreadyAllocated, "midiInOpen");

        Assert.Null(_opener.Open("DDJ-FLX4"));

        var line = Assert.Single(_logs.Lines);
        Assert.Equal("MIDI", line.Tag);
        Assert.Contains("in use by another app", line.Message);
        Assert.Contains("DDJ-FLX4", line.Message);
    }

    [Fact]
    public void An_already_allocated_output_logs_in_use_by_another_app_and_closes_the_input()
    {
        Plug();
        _devices.OutFails[0] = new MmException(MmResult.AlreadyAllocated, "midiOutOpen");

        Assert.Null(_opener.Open("DDJ-FLX4"));

        Assert.Contains("in use by another app", Assert.Single(_logs.Lines).Message);
        Assert.Equal(1, _devices.OpenedIns[0].Port.DisposeCount);
    }

    [Fact]
    public void A_busy_device_is_logged_once_per_busy_streak()
    {
        Plug();
        _devices.InFails[0] = new MmException(MmResult.AlreadyAllocated, "midiInOpen");

        _opener.Open("DDJ-FLX4");
        _opener.Open("DDJ-FLX4");
        _opener.Open("DDJ-FLX4");
        Assert.Single(_logs.Lines);

        // Freed and opened: the streak ends, so the next busy open is told again.
        _devices.InFails.Clear();
        Assert.NotNull(_opener.Open("DDJ-FLX4"));
        _devices.InFails[0] = new MmException(MmResult.AlreadyAllocated, "midiInOpen");
        _opener.Open("DDJ-FLX4");
        Assert.Equal(2, _logs.Lines.Count(l => l.Message.Contains("in use by another app")));
    }

    [Fact]
    public void Unplugging_ends_a_busy_streak()
    {
        Plug();
        _devices.InFails[0] = new MmException(MmResult.AlreadyAllocated, "midiInOpen");
        _opener.Open("DDJ-FLX4");

        _devices.Inputs.Clear();
        _devices.Outputs.Clear();
        _opener.Open("DDJ-FLX4");
        Plug();
        _opener.Open("DDJ-FLX4");

        Assert.Equal(2, _logs.Lines.Count(l => l.Message.Contains("in use by another app")));
    }

    [Fact]
    public void Another_open_failure_logs_could_not_open_with_the_name_and_message()
    {
        Plug();
        var failure = new MmException(MmResult.NoDriver, "midiInOpen");
        _devices.InFails[0] = failure;

        Assert.Null(_opener.Open("DDJ-FLX4"));

        var line = Assert.Single(_logs.Lines);
        Assert.Equal("MIDI", line.Tag);
        Assert.Equal($"could not open DDJ-FLX4: {failure.Message}", line.Message);
    }

    private void Plug()
    {
        _devices.Inputs.Add("DDJ-FLX4");
        _devices.Outputs.Add("DDJ-FLX4");
    }
}
