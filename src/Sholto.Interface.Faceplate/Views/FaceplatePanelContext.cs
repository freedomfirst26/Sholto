using Avalonia;
using Sholto.Interface.Faceplate.ViewModels;

namespace Sholto.Interface.Faceplate.Views;

/// <summary>What only exists once the overlay does: the host visual (for theme lookups), the
/// view model and the board. Built once in the overlay's constructor and handed to the panel
/// factories on every call, so those factories can be built before the overlay.</summary>
public sealed record FaceplatePanelContext(Visual Host, FaceplateViewModel ViewModel, FaceplateBoard Board)
{
    /// <summary>Which deck a per-deck control's chip should point at.
    /// <para><see cref="FaceplateViewModel.SelectedDeck"/> alone is wrong: it is -1
    /// whenever a GLOBAL control owns the panel, and a per-deck chip built with deck -1
    /// names a shape that does not exist — the drawing carries (id, 0) and (id, 1), never
    /// (id, -1). Clicking such a chip took <see cref="FaceplateBoard.OnSelectionChanged"/>'s
    /// missing-shape path: the panel swapped to the new control while the board dimmed
    /// and lit nothing at all. Reproduced by selecting FX ON/OFF (global) and clicking
    /// the HI chip in its prose.</para>
    /// <para>Deck 0 is the same fallback the board's own ResolveDeck already uses for the
    /// partner highlight, so hovering a row and clicking a chip inside it now agree —
    /// before this the hover lit deck 1's knob and the click lit nothing.</para></summary>
    public int ChipDeck => ViewModel.SelectedDeck >= 0 ? ViewModel.SelectedDeck : 0;

    /// <summary>The display name of a layer, falling back to its id.</summary>
    public string LayerNameOf(string layerId) =>
        ViewModel.Doc.Layers.FirstOrDefault(l => l.Id == layerId)?.Name ?? layerId;
}
