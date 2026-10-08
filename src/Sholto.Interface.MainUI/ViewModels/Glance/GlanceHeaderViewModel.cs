using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>The two LOAD TO slots and the ranking's reference deck. The slots are ticked by the frame clock while the overlay is open.</summary>
public sealed class GlanceHeaderViewModel :
    IGlanceHeaderViewModel,
    IFrameTickHandler,
    IEventHandler<LoadConfirmPending>
{
    /// <summary>Frame order: after the Glance view model's own tick.</summary>
    public const int ClockOrder = 110;

    private readonly IFrameClock _clock;
    private readonly IDeckSlot[] _slots;
    private bool _isOpen;
    private int _target;
    private int _replacePendingDeck = -1;

    public GlanceHeaderViewModel(
        IDeckClockSource decks, IFrameClock clock, IEventSubscriber subscriber, IDeckSlotFactory slotFactory)
    {
        _clock = clock;
        _slots = [slotFactory.Create(0), slotFactory.Create(1)];
        clock.Subscribe(this, ClockOrder);
        subscriber.Subscribe<LoadConfirmPending>(this);
    }

    public IReadOnlyList<IDeckSlotViewModel> Slots => _slots;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int ReferenceDeck { get; private set; } = -1;

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
        }

        UpdateSlots(_clock.Now);
    }

    public void OnFrame(DateTime now)
    {
        if (!_isOpen) return;
        UpdateSlots(now);
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
