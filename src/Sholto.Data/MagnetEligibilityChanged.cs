namespace Sholto.Data;

/// <summary>Magnet-lock eligibility flipped (the centerline magnet glyph follows it). State.</summary>
public readonly record struct MagnetEligibilityChanged(bool Eligible) : IStateEvent
{
    public int Slot => 0;
}
