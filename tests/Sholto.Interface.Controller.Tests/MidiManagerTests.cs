using Sholto.Interface.Controller;

namespace Sholto.Interface.Controller.Tests;

public sealed class MidiManagerTests : IDisposable
{
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(2);

    private readonly byte[] _init = [0x9F, 0x00, 0x7F];
    private readonly FakeRawMidiPort _port = new();
    private readonly FakeControllerMapping _mapping;
    private readonly MidiManager _manager;

    public MidiManagerTests()
    {
        _mapping = new FakeControllerMapping("FAKE-DEVICE", _init);
        _manager = new MidiManager(new MappingRegistry([_mapping]), new FakeMidiPortOpener(_port));
    }

    public void Dispose() => _manager.Dispose();

    [Fact]
    public void Connect_opens_the_mappings_device_and_sends_its_startup_init_through_the_port()
    {
        var connected = 0;
        _manager.Connected += () => connected++;

        Assert.True(_manager.Connect());

        Assert.True(_manager.IsConnected);
        Assert.Same(_mapping, _manager.Mapping);
        Assert.Equal(1, connected);
        Assert.Equal(_init, Assert.Single(_port.Sent));
    }

    [Fact]
    public void A_note_on_with_velocity_zero_reaches_the_mapping_as_a_release()
    {
        var events = new List<ControllerEvent>();
        _manager.EventReceived += events.Add;
        _manager.Connect();

        _port.Receive(0x90, 0x0B, 0x00);

        var note = Assert.Single(_mapping.Notes);
        Assert.Equal(new NoteEvent(Channel: 1, Key: 0x0B, Velocity: 0, IsDown: false), note);
        Assert.Single(events);
    }

    [Fact]
    public void A_note_on_with_velocity_reaches_the_mapping_as_a_press()
    {
        _manager.Connect();

        _port.Receive(0x91, 0x0B, 0x7F);

        Assert.Equal(new NoteEvent(Channel: 2, Key: 0x0B, Velocity: 0x7F, IsDown: true), Assert.Single(_mapping.Notes));
    }

    [Fact]
    public void Send_forwards_bytes_to_the_port()
    {
        _manager.Connect();
        byte[] led = [0x90, 0x0B, 0x7F];

        _manager.Send(led);

        Assert.Equal(led, _port.Sent[^1]);
        Assert.Equal(2, _port.Sent.Count);
    }

    [Fact]
    public async Task A_lost_port_raises_ConnectionLost_disconnects_and_disposes_the_port()
    {
        var lost = 0;
        _manager.ConnectionLost += () => lost++;
        _manager.Connect();

        _port.Lose();

        Assert.Equal(1, lost);
        Assert.False(_manager.IsConnected);
        await _port.Disposed.WaitAsync(DisposeTimeout);
    }

    [Fact]
    public void Dispose_disposes_the_port_without_raising_ConnectionLost()
    {
        // Its own manager: this test is the one Dispose call it gets.
        var port = new FakeRawMidiPort();
        var manager = new MidiManager(new MappingRegistry([_mapping]), new FakeMidiPortOpener(port));
        var lost = 0;
        manager.ConnectionLost += () => lost++;
        manager.Connect();

        manager.Dispose();

        Assert.True(port.IsDisposed);
        Assert.Equal(0, lost);
    }

    [Fact]
    public void Connect_returns_false_when_no_mapping_has_a_device()
    {
        var other = new FakeControllerMapping("OTHER-DEVICE", null);
        var opener = new FakeMidiPortOpener(null);
        using var manager = new MidiManager(new MappingRegistry([_mapping, other]), opener);

        Assert.False(manager.Connect());

        Assert.False(manager.IsConnected);
        Assert.Null(manager.Mapping);
        Assert.Equal(["FAKE-DEVICE", "OTHER-DEVICE"], opener.Requested);
    }
}
