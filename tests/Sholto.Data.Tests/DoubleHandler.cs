using Sholto.Data;

namespace Sholto.Data.Tests;

public sealed class DoubleHandler : IQueryHandler<Double, int>
{
    public int Handle(in Double query) => query.Value * 2;
}
