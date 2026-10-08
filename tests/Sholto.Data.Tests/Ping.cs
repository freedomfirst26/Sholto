using Sholto.Data;

namespace Sholto.Data.Tests;

public readonly record struct Ping(int Value, Origin Origin) : ICommand;
