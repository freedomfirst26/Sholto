namespace Sholto.Controller;

/// <summary>A fader with soft-takeover (pickup). The FLX-4 is a software mixer —
/// it only reports a fader's position when moved — so at startup the physical
/// position is unknown. Rather than jumping the level the instant the fader is
/// touched, the fader stays DISENGAGED and ignores moves until the physical
/// position <em>crosses</em> the current software value; then it engages and
/// tracks. That means bringing a fader up from the bottom smoothly introduces a
/// track, and there is never a sudden jump when software and hardware disagree.</summary>
public sealed class Fader : AnalogueControl
{
    private float _value;          // authoritative software value, 0..1
    private bool _engaged;
    private float? _lastPhysical;

    public float Value => _value;
    public bool Engaged => _engaged;

    /// <summary>Fires with the new value only while engaged (after pickup).</summary>
    public event Action<float>? ValueChanged;

    public Fader(string name) : base(name) { }

    /// <summary>Report a physical fader position in [0,1].</summary>
    public void Move(float physical)
    {
        physical = Math.Clamp(physical, 0f, 1f);

        if (!_engaged)
        {
            // Engage the moment the physical position crosses (or lands on) the
            // current software value. Needs a previous sample to detect a cross.
            if (_lastPhysical is float prev &&
                ((prev <= _value && physical >= _value) || (prev >= _value && physical <= _value)))
            {
                _engaged = true;
            }
            _lastPhysical = physical;
            if (!_engaged) return; // still catching up — ignore the move
        }

        if (_value != physical)
        {
            _value = physical;
            ValueChanged?.Invoke(_value);
        }
    }

}
