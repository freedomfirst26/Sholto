using Sholto.App.Audio;

namespace Sholto.App.Decks;

/// <summary>One deck's tune editor and two-point grid-edit mode.</summary>
public sealed class DeckTuneEditor(IDeckBeatgrid beatgrid) : IDeckTuneEditor
{
    private readonly IDeckBeatgrid _beatgrid = beatgrid;

    private bool _gridEditActive;
    private double? _gridAnchorA;
    private bool _editOpen;

    public event Action<DeckChange>? Changed;

    // Two-point grid edit: 'G' toggles the mode; the first waveform click stores anchor A, the second
    // computes the exact BPM from the span and applies it, then exits the mode.
    public bool GridEditActive
    {
        get => _gridEditActive;
        private set
        {
            if (_gridEditActive == value) return;
            _gridEditActive = value;
            Changed?.Invoke(DeckChange.GridEdit);
        }
    }

    public void ToggleGridEdit()
    {
        _gridAnchorA = null;
        GridEditActive = !GridEditActive;
        Console.WriteLine(GridEditActive
            ? "[Deck] grid edit ON - click an early kick, then a later kick"
            : "[Deck] grid edit OFF");
    }

    public void OnGridClick(double seconds)
    {
        if (!GridEditActive) return;
        if (_gridAnchorA is null)
        {
            _gridAnchorA = seconds;
            Console.WriteLine($"[Deck] grid anchor A = {seconds:F3}s - now click a later kick");
        }
        else
        {
            _beatgrid.SetGridFromTwoPoints(_gridAnchorA.Value, seconds);
            _gridAnchorA = null;
            GridEditActive = false;
        }
    }

    // Tune editor: click the BPM and one combined editor slides out over the disc. While it is open the
    // up/down keys adjust BPM and left/right nudge the grid.
    public bool EditOpen
    {
        get => _editOpen;
        private set
        {
            if (_editOpen == value) return;
            _editOpen = value;
            Changed?.Invoke(DeckChange.EditOpen);
        }
    }

    public void ToggleEdit() => EditOpen = !EditOpen;
    public void OpenEdit() => EditOpen = true;
    public void CloseEdit() => EditOpen = false;
}
