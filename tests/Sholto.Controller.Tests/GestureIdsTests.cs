using Sholto.Controller.Gestures;
using Xunit;

namespace Sholto.Controller.Tests;

public class GestureIdsTests
{
    [Fact]
    public void All_has_no_duplicates()
    {
        Assert.Equal(GestureIds.All.Count, GestureIds.All.Distinct().Count());
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
        Assert.Equal(declared.OrderBy(x => x), GestureIds.All.OrderBy(x => x));
    }

    [Fact]
    public void Every_id_is_lowercase_dotted()
    {
        foreach (var id in GestureIds.All)
            Assert.Matches("^[a-z0-9]+(\\.[a-z0-9]+)+$", id);
    }
}
