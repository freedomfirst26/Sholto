namespace Sholto.Data;

/// <summary>The harmony anchor key (deck 1's loaded key, else deck 2's), or null when neither has one. State.</summary>
public readonly record struct HarmonyReferenceChanged(KeyRef? Key) : IStateEvent
{
    public int Slot => 0;
}
