namespace Sholto.Interface.Controller;

public readonly record struct NoteEvent(int Channel, int Key, int Velocity, bool IsDown);
