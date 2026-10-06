using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Projection of the ranking's reference and the reference deck's playhead. The countdown is
/// ticked by the frame clock while the overlay is open and notifies only when the whole second changes.</summary>
public sealed class GlanceHeaderViewModel :
    IGlanceHeaderViewModel,
    IFrameTickHandler,
    IEventHandler<LibraryFilterChanged>,
    IEventHandler<LoadConfirmPending>
{
    /// <summary>Frame order: after the Glance view model's own tick.</summary>
    public const int ClockOrder = 110;

    private const double LowSeconds = 45;

    private readonly IDeckClockSource _decks;
    private readonly IFrameClock _clock;
    private readonly IDeckSlot[] _slots;
    private bool _isOpen;
    private int _target;
    private int _replacePendingDeck = -1;

    public GlanceHeaderViewModel(
        IDeckClockSource decks, IFrameClock clock, IEventSubscriber subscriber, IDeckSlotFactory slotFactory)
    {
        _decks = decks;
        _clock = clock;
        _slots = [slotFactory.Create(0), slotFactory.Create(1)];
        clock.Subscribe(this, ClockOrder);
        subscriber.Subscribe<LibraryFilterChanged>(this);
        subscriber.Subscribe<LoadConfirmPending>(this);
    }

    public IReadOnlyList<IDeckSlotViewModel> Slots => _slots;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HasReference => ReferenceDeck >= 0;

    public int ReferenceDeck { get; private set; } = -1;

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set { if (_isPlaying == value) return; _isPlaying = value; Notify(); }
    }

    private string _keyText = "";
    public string KeyText
    {
        get => _keyText;
        private set { if (_keyText == value) return; _keyText = value; Notify(); }
    }

    private string _bpmText = "";
    public string BpmText
    {
        get => _bpmText;
        private set { if (_bpmText == value) return; _bpmText = value; Notify(); }
    }

    private string _title = "";
    public string Title
    {
        get => _title;
        private set { if (_title == value) return; _title = value; Notify(); }
    }

    private string _countdownText = "";
    public string CountdownText
    {
        get => _countdownText;
        private set { if (_countdownText == value) return; _countdownText = value; Notify(); }
    }

    private bool _isLow;
    public bool IsLow
    {
        get => _isLow;
        private set { if (_isLow == value) return; _isLow = value; Notify(); }
    }

    public string? FilterLabel { get; private set; }

    public void Handle(in LibraryFilterChanged e)
    {
        FilterLabel = e.Label;
        Notify(nameof(FilterLabel));
    }

    /// <summary>The first replace press: the slot shows "press again" and the target follows the deck, as the
    /// Glance view model's does.</summary>
    public void Handle(in LoadConfirmPending e)
    {
        _replacePendingDeck = e.Pending ? e.Deck : -1;
        if (e.Pending) _target = e.Deck == 1 ? 1 : 0;
        UpdateSlots(_clock.Now);
    }

    public void SetOpen(bool isOpen)
    {
        _isOpen = isOpen;
        if (isOpen) UpdateSlots(_clock.Now);
    }

    private void UpdateSlots(DateTime now)
    {
        for (var i = 0; i < _slots.Length; i++)
            _slots[i].Update(_target == i, ReferenceDeck == i, _replacePendingDeck == i, now);
    }

    public void Apply(RankedTracks ranked, int targetDeck)
    {
        _target = targetDeck;
        var deck = ranked.ReferenceDeck;
        var changedDeck = deck != ReferenceDeck;
        ReferenceDeck = deck;
        if (changedDeck)
        {
            Notify(nameof(ReferenceDeck));
            Notify(nameof(HasReference));
        }

        KeyText = ranked.ReferenceKey?.ToCamelot() ?? "";
        BpmText = ranked.ReferenceBpm is { } b ? b.ToString("F1", CultureInfo.InvariantCulture) : "";

        if (deck >= 0)
        {
            var reading = _decks.Read(deck);
            IsPlaying = reading.IsPlaying;
            Title = reading.Title ?? ranked.Rows.FirstOrDefault(r => r.IsReference).Summary?.Title ?? "";
            ShowCountdown(reading);
        }
        else
        {
            IsPlaying = false;
            Title = "";
            CountdownText = "";
            IsLow = false;
        }
        UpdateSlots(_clock.Now);
    }

    public void OnFrame(DateTime now)
    {
        if (!_isOpen) return;
        UpdateSlots(now);
        if (ReferenceDeck < 0) return;
        var reading = _decks.Read(ReferenceDeck);
        IsPlaying = reading.IsPlaying;
        ShowCountdown(reading);
    }

    private void ShowCountdown(DeckClockReading reading)
    {
        if (!reading.IsPlaying)
        {
            CountdownText = "";
            IsLow = false;
            return;
        }
        var speed = reading.PlaybackSpeed > 0 ? reading.PlaybackSpeed : 1.0;
        var remaining = Math.Max(0, (reading.DurationSeconds - reading.PlaybackSeconds) / speed);
        CountdownText = remaining.ToRemainingText();
        IsLow = remaining < LowSeconds;
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
