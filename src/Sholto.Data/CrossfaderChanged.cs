namespace Sholto.Data;

/// <summary>The crossfader moved: 0 (full deck 1) to 1 (full deck 2). State.</summary>
public readonly record struct CrossfaderChanged(double Position) : IStateEvent
{
    public int Slot => 0;
}
