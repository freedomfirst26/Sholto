namespace Sholto.Data;

/// <summary>A musical key as it crosses the bus: pitch class (0 = C ... 11 = B) and mode. An interface
/// that has its own key type converts at its edge.</summary>
public readonly record struct KeyRef(int PitchClass, bool IsMajor);
