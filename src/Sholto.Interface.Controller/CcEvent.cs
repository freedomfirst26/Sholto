namespace Sholto.Interface.Controller;

public readonly record struct CcEvent(int Channel, int Control, int Value);
