using Microsoft.Extensions.Options;
using NAudio;
using Sholto.Data;

namespace Sholto.Interface.Controller.WinMm;

/// <summary>Windows implementation of <see cref="IMidiPortOpener"/>: opens the first WinMM input and the first
/// WinMM output whose product names contain the requested name, as one <see cref="WinMmMidiPort"/>. Never throws:
/// a missing or unopenable side is logged (a busy device once per busy streak) and gives null, and
/// <see cref="MidiManager"/>'s supervisor tries again.</summary>
public sealed class WinMmMidiPortOpener : IMidiPortOpener
{
    private readonly IWinMmDevices _devices;
    private readonly TimeSpan _pollInterval;
    private readonly TimeProvider _time;
    private readonly ILog _log;
    private bool _busyLogged;

    public WinMmMidiPortOpener(IWinMmDevices devices, IOptions<WinMmMidiOptions> options, TimeProvider time, ILogFactory logs)
    {
        _devices = devices;
        _pollInterval = options.Value.PollInterval;
        _time = time;
        _log = logs.Create("MIDI");
    }

    public IRawMidiPort? Open(string deviceName)
    {
        IReadOnlyList<string> inputs, outputs;
        try
        {
            inputs = _devices.InputNames();
            outputs = _devices.OutputNames();
        }
        catch (Exception ex)
        {
            _log.Write(LogLevel.Warn, $"could not list MIDI devices: {ex.Message}");
            return null;
        }

        int inIndex = IndexOf(inputs, deviceName);
        int outIndex = IndexOf(outputs, deviceName);
        if (inIndex < 0 || outIndex < 0)
        {
            _busyLogged = false;                                  // not listed: a later busy open is a new streak
            return null;
        }

        string inName = inputs[inIndex];
        string outName = outputs[outIndex];
        var input = TryOpen(inName, () => _devices.OpenIn(inIndex));
        if (input is null) return null;
        var output = TryOpen(outName, () => _devices.OpenOut(outIndex));
        if (output is null)
        {
            input.Dispose();
            return null;
        }

        var port = TryOpen(inName, () => new WinMmMidiPort(input, output, inName, outName, _devices, _pollInterval, _time, _log));
        if (port is null)
        {
            input.Dispose();
            output.Dispose();
            return null;
        }
        _busyLogged = false;
        return port;
    }

    private T? TryOpen<T>(string name, Func<T> open) where T : class
    {
        try
        {
            return open();
        }
        catch (MmException ex) when (ex.Result == MmResult.AlreadyAllocated)
        {
            if (!_busyLogged)
                _log.Write(LogLevel.Warn, $"{name} is in use by another app; close it there to use it in Sholto");
            _busyLogged = true;
            return null;
        }
        catch (Exception ex)
        {
            _log.Write(LogLevel.Warn, $"could not open {name}: {ex.Message}");
            return null;
        }
    }

    private int IndexOf(IReadOnlyList<string> names, string deviceName)
    {
        for (int i = 0; i < names.Count; i++)
            if (names[i].Contains(deviceName, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}
