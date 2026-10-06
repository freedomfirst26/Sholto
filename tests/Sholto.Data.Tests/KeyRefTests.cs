namespace Sholto.Data.Tests;

public class KeyRefTests
{
    [Theory]
    [InlineData(0, true, "8B")]
    [InlineData(1, true, "3B")]
    [InlineData(2, true, "10B")]
    [InlineData(3, true, "5B")]
    [InlineData(4, true, "12B")]
    [InlineData(5, true, "7B")]
    [InlineData(6, true, "2B")]
    [InlineData(7, true, "9B")]
    [InlineData(8, true, "4B")]
    [InlineData(9, true, "11B")]
    [InlineData(10, true, "6B")]
    [InlineData(11, true, "1B")]
    [InlineData(0, false, "5A")]
    [InlineData(1, false, "12A")]
    [InlineData(2, false, "7A")]
    [InlineData(3, false, "2A")]
    [InlineData(4, false, "9A")]
    [InlineData(5, false, "4A")]
    [InlineData(6, false, "11A")]
    [InlineData(7, false, "6A")]
    [InlineData(8, false, "1A")]
    [InlineData(9, false, "8A")]
    [InlineData(10, false, "3A")]
    [InlineData(11, false, "10A")]
    public void ToCamelot_renders_the_wheel_position(int pitchClass, bool isMajor, string expected)
    {
        Assert.Equal(expected, new KeyRef(pitchClass, isMajor).ToCamelot());
    }
}
