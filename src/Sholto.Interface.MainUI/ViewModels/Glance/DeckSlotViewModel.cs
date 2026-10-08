using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using Microsoft.Extensions.Options;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>The state of one LOAD TO slot, read off its deck each frame the header ticks. A steady frame
/// allocates nothing: strings are rebuilt only when the tempo or a state changes, and the
/// ring and the spin move in whole steps (1/360) and raise <see cref="Changed"/>, not a property change: the
/// platter reads them when it repaints.
/// <para>The platter turns once per bar. With a bar grid it follows the deck's playhead, so it keeps time with
/// the tempo fader and a scratch; without one it turns smoothly at the deck's tempo (120 BPM when unknown).
/// Under reduced motion it never turns.</para></summary>
public sealed class DeckSlotViewModel(int deck, IDeckClockSource decks, IMotionPreference motion, IOptions<GlanceViewOptions> options) : IDeckSlot
{
    /// <summary>The ring and the spin move in steps of 1/360, one degree.</summary>
    public const int Steps = 360;

    private const double BeatsPerBar = 4;
    private const double LongestFrameSeconds = 0.1;

    private readonly int _deck = deck;
    private readonly IDeckClockSource _decks = decks;
    private readonly IMotionPreference _motion = motion;
    private readonly double _fallbackBpm = options.Value.FallbackBpm;
    private readonly double _lowSeconds = options.Value.LowTimeSeconds;

    private int _ringStep;
    private int _spinStep;
    private double _spinTurns;
    private DateTime _lastFrame;
    private double _bpm = double.NaN;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? Changed;

    public int Number => _deck + 1;

    public bool IsLoaded { get; private set; }

    public bool IsPlaying { get; private set; }

    public bool IsTarget { get; private set; }

    public bool IsReference { get; private set; }

    public bool IsArmed { get; private set; }

    public bool IsCaution => IsTarget && IsPlaying && !IsArmed;

    public string Title { get; private set; } = "Empty";

    public string Camelot { get; private set; } = "";

    public IBrush? KeyBrush { get; private set; }

    public string BpmText { get; private set; } = "";

    public bool IsLow { get; private set; }

    public string CautionText => "Playing";

    public bool ShowsKeyCap => IsLoaded;

    public string KeyCapLabel => IsTarget ? $"⏎ ⇧{Number}" : $"⇧{Number}";

    public string ArmedText => $"⇧{Number} again to replace";

    public string EmptyTitle => $"DECK {Number} · empty";

    public string EmptyHint => IsTarget ? "Loads here" : "to load";

    public string KeyCapText => $"⇧{Number}";

    public bool IsSweeping => !IsLoaded && IsTarget && !_motion.Reduced;

    public bool ShowStats => IsLoaded && !IsCaution && !IsArmed;

    public bool ShowCaution => IsCaution;

    public bool ShowArmed => IsArmed;

    public double RemainingFraction => _ringStep / (double)Steps;

    public double SpinTurns => _spinStep / (double)Steps;

    public void Update(bool isTarget, bool isReference, bool replacePending, DateTime now)
    {
        var r = _decks.Read(_deck);
        var elapsed = _lastFrame == default ? 0 : Math.Clamp((now - _lastFrame).TotalSeconds, 0, LongestFrameSeconds);
        _lastFrame = now;

        var stateChanged = false;
        if (IsLoaded != r.IsLoaded)
        {
            IsLoaded = r.IsLoaded;
            stateChanged = true;
            Notify(nameof(IsLoaded));
            Notify(nameof(ShowsKeyCap));
            Notify(nameof(IsSweeping));
        }
        if (IsPlaying != r.IsPlaying)
        {
            IsPlaying = r.IsPlaying;
            stateChanged = true;
            Notify(nameof(IsPlaying));
        }
        if (IsTarget != isTarget)
        {
            IsTarget = isTarget;
            stateChanged = true;
            Notify(nameof(IsTarget));
            Notify(nameof(IsSweeping));
            Notify(nameof(EmptyHint));
            Notify(nameof(KeyCapLabel));
        }
        if (IsReference != isReference)
        {
            IsReference = isReference;
            stateChanged = true;
            Notify(nameof(IsReference));
        }
        var armed = replacePending && IsLoaded && IsPlaying && IsTarget;
        if (IsArmed != armed)
        {
            IsArmed = armed;
            stateChanged = true;
            Notify(nameof(IsArmed));
            Notify(nameof(ShowArmed));
        }
        if (stateChanged)
        {
            Notify(nameof(IsCaution));
            Notify(nameof(ShowCaution));
            Notify(nameof(ShowStats));
        }

        var visual = stateChanged;
        var title = r.IsLoaded ? r.Title ?? "" : "Empty";
        if (Title != title)
        {
            Title = title;
            Notify(nameof(Title));
            visual = true;
        }
        if (Camelot != r.Camelot)
        {
            Camelot = r.Camelot;
            Notify(nameof(Camelot));
            visual = true;
        }
        if (!ReferenceEquals(KeyBrush, r.KeyBrush))
        {
            KeyBrush = r.KeyBrush;
            Notify(nameof(KeyBrush));
            visual = true;
        }
        if (!_bpm.Equals(r.Bpm))
        {
            _bpm = r.Bpm;
            BpmText = r.Bpm > 0 ? r.Bpm.ToString("F1", CultureInfo.InvariantCulture) : "";
            Notify(nameof(BpmText));
            visual = true;
        }

        var speed = r.PlaybackSpeed > 0 ? r.PlaybackSpeed : 1.0;
        var remaining = r.IsLoaded ? Math.Max(0, (r.DurationSeconds - r.PlaybackSeconds) / speed) : 0;
        var low = r.IsLoaded && r.IsPlaying && remaining < _lowSeconds;
        if (IsLow != low)
        {
            IsLow = low;
            Notify(nameof(IsLow));
            visual = true;
        }

        var fraction = r.IsLoaded && r.DurationSeconds > 0
            ? Math.Clamp((r.DurationSeconds - r.PlaybackSeconds) / r.DurationSeconds, 0, 1)
            : 0;
        var ring = (int)Math.Round(fraction * Steps);
        if (ring != _ringStep)
        {
            _ringStep = ring;
            visual = true;
        }

        if (Spin(r, elapsed)) visual = true;
        if (visual) Changed?.Invoke();
    }

    /// <summary>Move the platter for this frame; true when it moved a whole step.</summary>
    private bool Spin(DeckClockReading r, double elapsed)
    {
        if (_motion.Reduced)
        {
            _spinTurns = 0;
        }
        else if (r.IsPlaying)
        {
            if (r.BarPeriodSeconds > 0)
            {
                var bars = (r.PlaybackSeconds - r.FirstDownbeatSeconds) / r.BarPeriodSeconds;
                _spinTurns = bars - Math.Floor(bars);
            }
            else
            {
                var bpm = r.Bpm > 0 ? r.Bpm : _fallbackBpm;
                var turns = _spinTurns + elapsed * bpm / (60 * BeatsPerBar);
                _spinTurns = turns - Math.Floor(turns);
            }
        }
        var step = (int)(_spinTurns * Steps) % Steps;
        if (step == _spinStep) return false;
        _spinStep = step;
        return true;
    }

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
