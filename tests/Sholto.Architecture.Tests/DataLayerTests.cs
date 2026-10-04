namespace Sholto.Architecture.Tests;

/// <summary>C55 dependency rule for the bus: Sholto.Data depends on nothing else in Sholto.</summary>
public sealed class DataLayerTests
{
    private readonly AssemblyReferences _references = new();

    [Fact]
    public void DataReferencesNoOtherSholtoAssembly()
    {
        var offending = _references.Of("Sholto.Data")
            .Where(reference => _references.IsUnder(reference, "Sholto"))
            .ToList();

        Assert.True(offending.Count == 0, $"Sholto.Data must not reference: {string.Join(", ", offending)}");
    }
}
