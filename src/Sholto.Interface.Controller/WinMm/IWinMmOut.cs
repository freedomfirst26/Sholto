namespace Sholto.Interface.Controller.WinMm;

/// <summary>One open WinMM MIDI output. Disposing closes it.</summary>
public interface IWinMmOut : IDisposable
{
    /// <summary>Send one packed short message (status in bits 0–7, data1 in 8–15, data2 in 16–23).</summary>
    void Send(int message);
}
