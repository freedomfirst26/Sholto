using Microsoft.Extensions.Options;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Builds <see cref="DeckSlotViewModel"/>s over one deck clock source and one motion preference.</summary>
public sealed class DeckSlotFactory(IDeckClockSource decks, IMotionPreference motion, IOptions<GlanceViewOptions> options) : IDeckSlotFactory
{
    private readonly IDeckClockSource _decks = decks;
    private readonly IMotionPreference _motion = motion;
    private readonly IOptions<GlanceViewOptions> _options = options;

    public IDeckSlot Create(int deck) => new DeckSlotViewModel(deck, _decks, _motion, _options);
}
