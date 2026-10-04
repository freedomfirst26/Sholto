using Sholto.Data;

namespace Sholto.Data.Tests;

public readonly record struct Fact(int Value) : IEvent;
