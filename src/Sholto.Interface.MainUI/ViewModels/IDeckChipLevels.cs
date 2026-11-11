namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The stem chips of one deck: which stems are active and how bright each chip is.</summary>
public interface IDeckChipLevels
{
    bool DrumsActive { get; }
    bool VocalsActive { get; }
    bool InstrumentalActive { get; }

    /// <summary>Chip opacity per stem: the level (0..1) mapped to <see cref="MinOpacity"/>..1 so a turned-down
    /// stem stays readable. A muted chip stays at 1 so its hollow look is distinct from a low level.</summary>
    double DrumsChipOpacity { get; }
    double VocalsChipOpacity { get; }
    double InstrumentalChipOpacity { get; }

    /// <summary>Opacity of a stem chip turned fully down (from <c>DeckViewOptions</c>).</summary>
    double MinOpacity { get; }

    /// <summary>Stores whether the stem is active; false for a stem outside 0..2 (nothing stored).</summary>
    bool SetActive(int stem, bool active);

    /// <summary>Stores the stem's level; true only when that chip's opacity changed. False for a stem
    /// outside 0..2.</summary>
    bool SetLevel(int stem, double level);
}
