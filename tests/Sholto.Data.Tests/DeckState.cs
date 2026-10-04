using Sholto.Data;

namespace Sholto.Data.Tests;

public readonly record struct DeckState(int Slot, int Value) : IStateEvent;
