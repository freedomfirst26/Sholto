using Sholto.Interface.Controller.Gestures;
using Xunit;

namespace Sholto.Interface.Controller.Tests;

public class GestureIdsTests
{
    private readonly IGestureCatalog _catalog = new GestureCatalog();

    [Fact]
    public void All_has_no_duplicates()
    {
        Assert.Equal(_catalog.All.Count, _catalog.All.Distinct().Count());
    }

    [Fact]
    public void All_contains_every_declared_id()
    {
        var declared = typeof(GestureIds)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(declared);
        Assert.Equal(declared.OrderBy(x => x), _catalog.All.OrderBy(x => x));
    }

    [Fact]
    public void Every_id_is_lowercase_dotted()
    {
        foreach (var id in _catalog.All)
            Assert.Matches("^[a-z0-9]+(\\.[a-z0-9]+)+$", id);
    }
}
