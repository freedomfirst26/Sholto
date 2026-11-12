namespace Sholto.Interface.Controller.WinMm;

/// <summary>One open WinMM MIDI input. Disposing stops and closes it.</summary>
public interface IWinMmIn : IDisposable
{
    /// <summary>A packed WinMM short message: status in bits 0–7, data1 in 8–15, data2 in 16–23. Raised on a
    /// WinMM callback thread.</summary>
    event Action<int>? MessageReceived;

    /// <summary>Start delivering <see cref="MessageReceived"/>.</summary>
    void Start();
}
