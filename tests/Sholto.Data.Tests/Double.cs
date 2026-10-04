using Sholto.Data;

namespace Sholto.Data.Tests;

public readonly record struct Double(int Value) : IQuery<int>;
