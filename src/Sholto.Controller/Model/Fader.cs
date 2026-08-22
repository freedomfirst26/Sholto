namespace Sholto.Controller;

/// <summary>A fader whose value is <c>null</c> until it has been measured. The
/// FLX-4 only reports a fader's position when it is moved, so at startup we
/// genuinely do not know where the fader sits — <c>null</c> is that "unmeasured"
/// truth, distinct from 0. The first move adopts the physical position (there is
/// no prior value to soft-takeover against); every move tracks it. The UI shows no
/// level until that first measurement (the deck itself plays at unity meanwhile, so
/// there's sound after a restart — see DeckViewModel.ApplyVolume).</summary>
public sealed class Fader : AnalogueControl
{
    private float? _value; // null = not yet measured

    /// <summary>Physical position in [0,1], or null until first moved.</summary>
    public float? Value => _value;

    /// <summary>True once the fader has been moved at least once.</summary>
    public bool Measured => _value.HasValue;

    /// <summary>Fires with the new value on every move (always a real value).</summary>
    public event Action<float>? ValueChanged;

    public Fader(string name) : base(name) { }

    /// <summary>Report a physical fader position in [0,1].</summary>
    public void Move(float physical)
    {
        physical = Math.Clamp(physical, 0f, 1f);
        if (_value == physical) return;
        _value = physical;
        ValueChanged?.Invoke(physical);
    }
}
