using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>The state machine behind a collapse-to-icon consumer. It decides when each phase ends; the
/// container and the icon wrapper do the pixels. Time comes from the frame clock, so a test drives it with a
/// fake one. A tick allocates nothing.</summary>
public sealed class CollapseToIconSequence(
    IFrameClock clock,
    IMotionPreference motion,
    CollapseToIconOptions options,
    IHintPolicy hintPolicy) : ICollapseToIconSequence, IFrameTickHandler
{
    private readonly IFrameClock _clock = clock;
    private readonly IMotionPreference _motion = motion;
    private readonly CollapseToIconOptions _options = options;
    private readonly IHintPolicy _hintPolicy = hintPolicy;
    private DateTime _phaseStart;

    public CollapseToIconState State { get; private set; } = CollapseToIconState.Idle;

    public bool IsShown => State is CollapseToIconState.Open or CollapseToIconState.Collapsing;

    public bool IsHintStatic => State == CollapseToIconState.Hinting && _motion.Reduced;

    public bool Reduced => _motion.Reduced;

    public CollapseToIconTimings Timings => _options.Timings;

    public event Action? Changed;

    private TimeSpan CollapseSpan => _motion.Reduced ? _options.Timings.ReducedFade : _options.Timings.Collapse;

    private TimeSpan HintSpan => _motion.Reduced
        ? _options.Timings.ReducedHintHold
        : _options.Timings.PulsePeriod * _options.Timings.PulseCount;

    public void Open() => Enter(CollapseToIconState.Open);

    public void Collapse()
    {
        if (State != CollapseToIconState.Open) return;
        _phaseStart = _clock.Now;
        Enter(CollapseToIconState.Collapsing);
    }

    public void TargetEngaged()
    {
        if (State == CollapseToIconState.Hinting) Enter(CollapseToIconState.Idle);
    }

    public void OnFrame(DateTime now)
    {
        if (State is CollapseToIconState.Open or CollapseToIconState.Idle) return;

        if (State == CollapseToIconState.Collapsing)
        {
            var collapseEnd = _phaseStart + CollapseSpan;
            if (now < collapseEnd) return;
            if (!_hintPolicy.ShouldHint())
            {
                Enter(CollapseToIconState.Idle);
                return;
            }
            // A stalled frame still starts the hint from where the collapse ended.
            _phaseStart = collapseEnd;
            _hintPolicy.HintShown();
            Enter(CollapseToIconState.Hinting);
        }

        if (now - _phaseStart >= HintSpan) Enter(CollapseToIconState.Idle);
    }

    private void Enter(CollapseToIconState state)
    {
        if (State == state) return;
        State = state;
        Changed?.Invoke();
    }
}
