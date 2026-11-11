namespace Sholto.Data;

/// <summary>A saved hot cue: pad 0-7, position in track seconds, and its colour.</summary>
public readonly record struct HotCue(int Pad, double PositionSec, CueColour Colour);
