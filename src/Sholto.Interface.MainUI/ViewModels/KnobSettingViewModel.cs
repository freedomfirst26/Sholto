using System.ComponentModel;
using System.Globalization;
using Sholto.Interface.MainUI.Controls.Knob;

namespace Sholto.Interface.MainUI.ViewModels;

/// <inheritdoc />
public sealed class KnobSettingViewModel : IKnobSettingViewModel
{
    private readonly string _format;
    private readonly string _unit;
    private readonly Action<double> _send;
    private double _value;
    private bool _isActive;

    public KnobSettingViewModel(IKnobScale scale, double defaultValue, string format, string unit, Action<double> send)
    {
        Scale = scale;
        Default = defaultValue;
        _format = format;
        _unit = unit;
        _send = send;
        _value = defaultValue;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IKnobScale Scale { get; }

    public double Default { get; }

    public double Value
    {
        get => _value;
        set
        {
            var snapped = Scale.Snap(value);
            if (!Apply(snapped)) return;
            _send(snapped);
        }
    }

    public string Text => _value.ToString(_format, CultureInfo.InvariantCulture) + _unit;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            Notify(nameof(IsActive));
        }
    }

    public void Step(int steps) => Value = Scale.Nudge(_value, steps);

    public void ToEdge(bool top) => Value = top ? Scale.Maximum : Scale.Minimum;

    public void Show(double value) => Apply(value);

    private bool Apply(double value)
    {
        if (value == _value) return false;
        _value = value;
        Notify(nameof(Value));
        Notify(nameof(Text));
        return true;
    }

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
