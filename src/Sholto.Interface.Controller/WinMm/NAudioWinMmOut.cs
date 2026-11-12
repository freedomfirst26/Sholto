using NAudio.Midi;

namespace Sholto.Interface.Controller.WinMm;

/// <summary>The real <see cref="IWinMmOut"/>: one NAudio <see cref="MidiOut"/>.</summary>
public sealed class NAudioWinMmOut(MidiOut output) : IWinMmOut
{
    private readonly MidiOut _out = output;

    public void Send(int message) => _out.Send(message);

    public void Dispose() => _out.Dispose();
}
