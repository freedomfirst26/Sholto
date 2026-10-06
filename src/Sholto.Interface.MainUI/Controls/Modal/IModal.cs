using System.ComponentModel;
using Avalonia.Input;

namespace Sholto.Interface.MainUI.Controls.Modal;

/// <summary>The minimum any in-window modal gives the key router and the scrim.</summary>
public interface IModal : INotifyPropertyChanged
{
    bool IsOpen { get; }

    ModalScrimClick ScrimClick { get; }

    /// <summary>True when unhandled keys belong to the focused text box rather than being swallowed.</summary>
    bool CapturesText { get; }

    /// <summary>Esc, a scrim press (when it dismisses) and the Dismiss button.</summary>
    void Dismiss();

    /// <summary>Content-specific keys (arrows, digits, Tab…). True means handled.</summary>
    bool HandleKey(Key key, KeyModifiers modifiers);
}
