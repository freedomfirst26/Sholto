namespace Sholto.Interface.MainUI;

/// <summary>Deck view settings, supplied via the standard <c>IOptions&lt;DeckViewOptions&gt;</c> pipeline like
/// <see cref="FeatureOptions"/>. Defaults live here.</summary>
public sealed class DeckViewOptions
{
    /// <summary>Opacity of a stem chip whose level is turned fully down (0..1), so a quiet stem stays readable.
    /// A muted chip stays at 1 so its hollow look differs from a low level.</summary>
    public double StemChipMinOpacity { get; set; } = 0.3;
}
