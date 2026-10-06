using System.ComponentModel;
using Sholto.Interface.MainUI.Controls.Knob;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>One knob card in the Settings overlay: a value that snaps to its scale, shows at once and is sent
/// to the App; the App's own value is shown with <see cref="Show"/> and never sent back.</summary>
public interface IKnobSettingViewModel : INotifyPropertyChanged
{
    /// <summary>The value, snapped to the scale. Setting it shows it and sends it (once, if it changed).</summary>
    double Value { get; set; }

    /// <summary>The value as the readout shows it, with its unit.</summary>
    string Text { get; }

    /// <summary>What a double-click resets to.</summary>
    double Default { get; }

    IKnobScale Scale { get; }

    /// <summary>True for the card the keyboard moves (outlined).</summary>
    bool IsActive { get; set; }

    /// <summary>Move <paramref name="steps"/> scale steps (the arrow keys).</summary>
    void Step(int steps);

    /// <summary>Jump to the minimum (false) or maximum (true): Home / End.</summary>
    void ToEdge(bool top);

    /// <summary>The App's value: shown, not sent.</summary>
    void Show(double value);
}
