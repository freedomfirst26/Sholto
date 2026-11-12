using NAudio.Midi;

namespace Sholto.Interface.Controller.WinMm;

/// <summary>The real <see cref="IWinMmIn"/>: one NAudio <see cref="MidiIn"/>, passing on each raw short message.</summary>
public sealed class NAudioWinMmIn : IWinMmIn
{
    private readonly MidiIn _in;

    public event Action<int>? MessageReceived;

    public NAudioWinMmIn(MidiIn input)
    {
        _in = input;
        _in.MessageReceived += OnMessage;
    }

    public void Start() => _in.Start();

    public void Dispose()
    {
        _in.MessageReceived -= OnMessage;
        try { _in.Stop(); }
        finally { _in.Dispose(); }
    }

    private void OnMessage(object? sender, MidiInMessageEventArgs e) => MessageReceived?.Invoke(e.RawMessage);
}
