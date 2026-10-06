using Sholto.Interface.MainUI.Controls.Modal;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The Settings overlay (Settings ▸ Settings…). Whether it is open is presentation state; the backspin
/// time and distance are the App's (<c>BackspinTimeChanged</c> / <c>BackspinDistanceChanged</c> in,
/// <c>SetBackspinTime</c> / <c>SetBackspinDistance</c> out), and this only mirrors them. A change applies live
/// and is saved by the App; there is no Apply/Cancel. As a modal it shows one Close button;
/// <see cref="IModal.Dismiss"/> is <see cref="Close"/>, and its own keys (Tab switches the active knob, ← ↓ / → ↑
/// step it, Home / End jump to its ends) are <see cref="IModal.HandleKey"/>.</summary>
public interface ISettingsViewModel : IModalContent
{
    void Open();

    void Close();

    /// <summary>How long a fling keeps spinning, in seconds (0..3, default 0.6).</summary>
    IKnobSettingViewModel Time { get; }

    /// <summary>How far a fling rewinds, in beats (0..16, default 2).</summary>
    IKnobSettingViewModel Distance { get; }

    /// <summary>The knob the keys move: <see cref="Time"/> first.</summary>
    IKnobSettingViewModel ActiveKnob { get; }
}
