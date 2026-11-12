using Sholto.Data;
using Sholto.Interface.Controller;

namespace Sholto.Interface.Controller.Tests;

/// <summary>Pins the lines <see cref="MidiManager"/> writes through its <see cref="ILog"/>s.</summary>
public sealed class MidiManagerLogTests : IDisposable
{
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(2);

    private readonly FakeRawMidiPort _port = new();
    private readonly RecordingLogFactory _logs = new();
    private readonly MidiManager _manager;

    public MidiManagerLogTests()
    {
        var mapping = new FakeControllerMapping("FAKE-DEVICE", null);
        _manager = new MidiManager(new MappingRegistry([mapping]), new FakeMidiPortOpener(_port), _logs);
    }

    public void Dispose() => _manager.Dispose();

    [Fact]
    public async Task A_lost_controller_logs_the_disconnect_line_as_a_MIDI_warning()
    {
        Assert.True(_manager.Connect());

        _port.Lose();
        await _port.Disposed.WaitAsync(DisposeTimeout);

        Assert.Contains(new LoggedLine("MIDI", LogLevel.Warn, "controller disconnected — retrying until it comes back"),
            _logs.Lines);
    }

    [Fact]
    public void Connecting_logs_the_device_and_mapping_as_MIDI_info()
    {
        Assert.True(_manager.Connect());

        Assert.Contains(new LoggedLine("MIDI", LogLevel.Info,
            $"connected to FAKE-DEVICE (mapping: {nameof(FakeControllerMapping)})"), _logs.Lines);
    }

    [Fact]
    public void A_raw_message_is_logged_under_MIDI_raw_only_while_raw_logging_is_on()
    {
        Assert.True(_manager.Connect());

        _port.Receive(0x90, 0x0B, 0x7F);
        Assert.DoesNotContain(_logs.Lines, l => l.Tag == "MIDI raw");

        _manager.LogAllMessages = true;
        _port.Receive(0x90, 0x0B, 0x7F);

        Assert.Contains(new LoggedLine("MIDI raw", LogLevel.Debug, "ch=01 NoteOn  key/cc=0x0B( 11) val=127"),
            _logs.Lines);
    }
}
