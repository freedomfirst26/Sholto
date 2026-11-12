using Microsoft.Extensions.Options;
using Sholto.Interface.Controller.WinMm;

namespace Sholto.Interface.Controller.Tests.WinMm;

/// <summary>An open WinMM port on fake devices: messages both ways, unplug noticed by the device-list poll, and a
/// quiet, repeatable dispose.</summary>
public sealed class WinMmMidiPortTests
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private readonly FakeWinMmDevices _devices = new();
    private readonly RecordingLogFactory _logs = new();
    private readonly ManualTimeProvider _time = new();
    private readonly IRawMidiPort _port;
    private readonly List<(byte, byte, byte)> _received = [];
    private int _disconnects;

    public WinMmMidiPortTests()
    {
        _devices.Inputs.AddRange(["Other In", "DDJ-FLX4"]);
        _devices.Outputs.AddRange(["Other Out", "DDJ-FLX4"]);
        var opener = new WinMmMidiPortOpener(_devices, Options.Create(new WinMmMidiOptions()), _time, _logs);
        _port = opener.Open("DDJ-FLX4")!;
        _port.MessageReceived += (s, d1, d2) => _received.Add((s, d1, d2));
        _port.Disconnected += () => _disconnects++;
    }

    private FakeWinMmIn In => _devices.OpenedIns[0].Port;
    private FakeWinMmOut Out => _devices.OpenedOuts[0].Port;

    [Fact]
    public void Incoming_short_messages_are_unpacked_to_status_and_data()
    {
        In.Receive(0x90 | 0x1B << 8 | 0x7F << 16);   // Note On
        In.Receive(0xB0 | 0x21 << 8 | 0x41 << 16);   // CC
        In.Receive(0xC3 | 0x05 << 8 | 0x66 << 16);   // Program Change: one data byte, data2 zeroed

        Assert.Equal([(0x90, 0x1B, 0x7F), (0xB0, 0x21, 0x41), (0xC3, 0x05, 0x00)], _received);
    }

    [Fact]
    public void Realtime_and_data_only_bytes_are_dropped()
    {
        In.Receive(0xF8);   // clock
        In.Receive(0xFE);   // active sensing
        In.Receive(0x40);   // data byte with no status

        Assert.Empty(_received);
    }

    [Fact]
    public void Send_splits_a_buffer_of_several_messages_into_packed_short_messages_in_order()
    {
        _port.Send([0x90, 0x1B, 0x7F, 0x90, 0x1B, 0x00, 0x91, 0x1B, 0x7F, 0xC0, 0x05]);

        Assert.Equal([0x7F1B90, 0x001B90, 0x7F1B91, 0x0005C0], Out.Sent);
    }

    [Fact]
    public void Send_expands_running_status()
    {
        _port.Send([0xB0, 0x01, 0x02, 0x03, 0x04]);

        Assert.Equal([0x0201B0, 0x0403B0], Out.Sent);
    }

    [Fact]
    public void Send_drops_a_truncated_tail_but_sends_what_came_before()
    {
        _port.Send([0x90, 0x1B, 0x7F, 0x91, 0x1B]);

        Assert.Equal([0x7F1B90], Out.Sent);
    }

    [Fact]
    public void Send_never_throws_when_the_output_fails()
    {
        Out.SendFails = new InvalidOperationException("write failed");

        _port.Send([0x90, 0x1B, 0x7F]);

        Assert.Contains(_logs.Lines, l => l.Tag == "MIDI" && l.Message.Contains("write failed"));
    }

    [Fact]
    public void The_device_list_is_polled_every_poll_interval()
    {
        var timer = Assert.Single(_time.Timers);
        Assert.Equal(Second, timer.Period);

        _time.Advance(Second * 3);

        Assert.Equal(3, timer.Fired);
        Assert.Equal(0, _disconnects);
    }

    [Fact]
    public void Disconnected_is_raised_once_within_one_poll_interval_of_the_input_leaving_the_list()
    {
        _devices.Inputs.Remove("DDJ-FLX4");

        _time.Advance(Second);
        Assert.Equal(1, _disconnects);

        _time.Advance(Second * 5);
        Assert.Equal(1, _disconnects);
    }

    [Fact]
    public void Disconnected_is_raised_when_the_output_leaves_the_list()
    {
        _devices.Outputs.Remove("DDJ-FLX4");

        _time.Advance(Second);

        Assert.Equal(1, _disconnects);
    }

    [Fact]
    public void The_device_is_matched_by_name_not_index()
    {
        // Another device unplugged: the controller shifts down one index but is still there.
        _devices.Inputs.RemoveAt(0);
        _devices.Outputs.RemoveAt(0);
        _time.Advance(Second);
        Assert.Equal(0, _disconnects);

        // The controller unplugged, and something else now sits at its old index.
        _devices.Inputs[0] = "Other In";
        _devices.Outputs[0] = "Other Out";
        _devices.Inputs.Add("Late In");
        _devices.Outputs.Add("Late Out");
        _time.Advance(Second);
        Assert.Equal(1, _disconnects);
    }

    [Fact]
    public void A_poll_that_throws_is_logged_and_the_device_kept()
    {
        _devices.ListFails = new InvalidOperationException("list failed");

        _time.Advance(Second);

        Assert.Equal(0, _disconnects);
        Assert.Contains(_logs.Lines, l => l.Message.Contains("list failed"));
    }

    [Fact]
    public void Dispose_closes_both_sides_once_and_never_raises_disconnected()
    {
        _port.Dispose();
        _port.Dispose();
        _devices.Inputs.Clear();
        _time.Advance(Second * 2);

        Assert.Equal(1, In.DisposeCount);
        Assert.Equal(1, Out.DisposeCount);
        Assert.True(_time.Timers[0].IsDisposed);
        Assert.Equal(0, _disconnects);
    }

    [Fact]
    public void Dispose_after_disconnected_still_closes_both_sides()
    {
        _devices.Inputs.Clear();
        _time.Advance(Second);
        _port.Dispose();

        Assert.Equal(1, _disconnects);
        Assert.Equal(1, In.DisposeCount);
        Assert.Equal(1, Out.DisposeCount);
    }

    [Fact]
    public async Task Dispose_is_safe_from_a_thread_pool_thread()
    {
        await Task.Run(_port.Dispose);

        Assert.Equal(1, In.DisposeCount);
        Assert.Equal(0, _disconnects);
    }

    [Fact]
    public void Nothing_is_received_or_sent_after_dispose()
    {
        _port.Dispose();

        In.Receive(0x90 | 0x1B << 8 | 0x7F << 16);
        _port.Send([0x90, 0x1B, 0x7F]);

        Assert.Empty(_received);
        Assert.Empty(Out.Sent);
    }
}
