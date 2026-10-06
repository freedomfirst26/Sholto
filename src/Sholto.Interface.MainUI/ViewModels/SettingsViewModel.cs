using System.ComponentModel;
using Avalonia.Input;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Knob;
using Sholto.Interface.MainUI.Controls.Modal;

namespace Sholto.Interface.MainUI.ViewModels;

/// <inheritdoc />
/// <remarks>Follows <see cref="BackspinTimeChanged"/> and <see cref="BackspinDistanceChanged"/> (state events, so
/// the current values arrive on subscribe). A user change is snapped and clamped by the knob's scale, shown at
/// once and sent as <see cref="SetBackspinTime"/> / <see cref="SetBackspinDistance"/>; the App's echo then
/// matches it and changes nothing.</remarks>
public sealed class SettingsViewModel : ISettingsViewModel, IEventHandler<BackspinTimeChanged>,
    IEventHandler<BackspinDistanceChanged>
{
    private bool _isOpen;
    private IKnobSettingViewModel _activeKnob;

    public SettingsViewModel(IEventSubscriber subscriber, ICommandSender sender, IKnobScaleFactory scales)
    {
        var timeOrigin = new Origin(InterfaceIds.MainUI, "settings.backspin-time", "turn");
        var distanceOrigin = new Origin(InterfaceIds.MainUI, "settings.backspin-distance", "turn");
        Time = new KnobSettingViewModel(scales.BackspinTime(), SetBackspinTime.Default, "0.00", " s",
            v => sender.Send(new SetBackspinTime(v, timeOrigin)));
        Distance = new KnobSettingViewModel(scales.BackspinDistance(), SetBackspinDistance.Default, "0.0#", " beats",
            v => sender.Send(new SetBackspinDistance(v, distanceOrigin)));
        _activeKnob = Time;
        Time.IsActive = true;
        subscriber.Subscribe<BackspinTimeChanged>(this);
        subscriber.Subscribe<BackspinDistanceChanged>(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (_isOpen == value) return;
            _isOpen = value;
            Notify(nameof(IsOpen));
        }
    }

    public void Open() => IsOpen = true;

    public void Close() => IsOpen = false;

    public string Eyebrow => "SETTINGS";

    public ModalTone Tone => ModalTone.Accent;

    public string Title => "Platter feel";

    public string? Subtitle => "Changes apply straight away and are remembered.";

    public string KeyHint =>
        "Drag or scroll a knob · Tab switches knob · ← → step · Home / End · double-click = default · Esc closes";

    public ModalWidth Width => ModalWidth.Regular;

    public ModalButtons Buttons { get; } = new("Close", null, null);

    public ModalScrimClick ScrimClick => ModalScrimClick.Dismisses;

    public bool CapturesText => false;

    public bool CanGoBack => false;

    public bool CanConfirm => false;

    public void Dismiss() => Close();

    public void Back() { }

    public void Confirm() { }

    /// <summary>Tab / Shift+Tab switch the active knob, ← ↓ step it down, → ↑ step it up, Home / End jump it to
    /// its ends. Esc is the router's.</summary>
    public bool HandleKey(Key key, KeyModifiers modifiers)
    {
        switch (key)
        {
            case Key.Tab: ActiveKnob = ReferenceEquals(_activeKnob, Time) ? Distance : Time; return true;
            case Key.Left: case Key.Down: _activeKnob.Step(-1); return true;
            case Key.Right: case Key.Up: _activeKnob.Step(+1); return true;
            case Key.Home: _activeKnob.ToEdge(false); return true;
            case Key.End: _activeKnob.ToEdge(true); return true;
            default: return false;
        }
    }

    public IKnobSettingViewModel Time { get; }

    public IKnobSettingViewModel Distance { get; }

    public IKnobSettingViewModel ActiveKnob
    {
        get => _activeKnob;
        private set
        {
            if (ReferenceEquals(_activeKnob, value)) return;
            _activeKnob.IsActive = false;
            _activeKnob = value;
            _activeKnob.IsActive = true;
            Notify(nameof(ActiveKnob));
        }
    }

    /// <summary>The App's value: shown, never sent back.</summary>
    public void Handle(in BackspinTimeChanged e) => Time.Show(e.Seconds);

    public void Handle(in BackspinDistanceChanged e) => Distance.Show(e.Beats);

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
