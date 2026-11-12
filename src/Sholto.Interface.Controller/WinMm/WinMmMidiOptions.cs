namespace Sholto.Interface.Controller.WinMm;

/// <summary>Tunables for the Windows (WinMM) MIDI port.</summary>
public sealed class WinMmMidiOptions
{
    /// <summary>How often an open port re-reads the WinMM device list to notice the controller was unplugged
    /// (WinMM itself says nothing). The longest a lost controller goes unnoticed.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
}
