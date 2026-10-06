namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>What the disc bloom draws: one glow opacity per waveform band, 0 to 1 (already scaled and
/// clamped), and a notification that fires only when a visible step happened.</summary>
public interface IDiscBloomLevels
{
    /// <summary>Opacity of the large bottom-left glow (bass band).</summary>
    double Low { get; }

    /// <summary>Opacity of the medium right-hand glow (mid band).</summary>
    double Mid { get; }

    /// <summary>Opacity of the small top glow (high band).</summary>
    double High { get; }

    /// <summary>Raised when any opacity moved by at least one 8-bit step.</summary>
    event Action? Changed;
}
