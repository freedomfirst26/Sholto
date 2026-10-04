namespace Sholto.Data;

/// <summary>MASTER CUE (the master mix folded into the headphone cue) was switched on or off.</summary>
public readonly record struct MasterCueChanged(bool On) : IStateEvent
{
    public int Slot => 0;
}
