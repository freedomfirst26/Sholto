namespace Sholto.Data;

/// <summary>The harmony anchor key (deck 1's loaded key, else deck 2's), or null when neither has one, and
/// the keys that mix with it. State.
/// <para><paramref name="MixableKeys"/> is empty when <paramref name="Key"/> is null. The list is shared by
/// reference with every subscriber, so do not change it. A <c>default</c> struct carries a null list:
/// treat that as empty.</para></summary>
public readonly record struct HarmonyReferenceChanged(KeyRef? Key, IReadOnlyList<KeyRef> MixableKeys) : IStateEvent
{
    public int Slot => 0;
}
