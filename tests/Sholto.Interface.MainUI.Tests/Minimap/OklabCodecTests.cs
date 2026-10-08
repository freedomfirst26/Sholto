using Avalonia.Media;

namespace Sholto.Interface.MainUI.Tests.Minimap;

public sealed class OklabCodecTests
{
    private readonly OklabCodec _codec = new();

    [Fact]
    public void Red_FF3D3D_IsLightness066_Chroma0229_Hue26()
    {
        var lab = _codec.Encode(Color.Parse("#FF3D3D"));

        Assert.InRange(lab.L, 0.655, 0.665);
        Assert.InRange(_codec.Chroma(lab), 0.226, 0.232);
        Assert.InRange(_codec.HueDegrees(lab), 25.0, 27.5);
    }

    [Fact]
    public void WhiteAndBlack_AreLightnessOneAndZero()
    {
        Assert.Equal(1.0, _codec.Encode(Colors.White).L, 3);
        Assert.Equal(0.0, _codec.Encode(Colors.Black).L, 3);
    }

    [Theory]
    [InlineData("#FF3D3D")]
    [InlineData("#246CFF")]
    [InlineData("#C6FF1A")]
    public void EncodeThenDecode_ReturnsTheSameColour(string hex)
    {
        var c = Color.Parse(hex);
        var lab = _codec.Encode(c);

        Assert.Equal(c, _codec.Decode(lab.L, lab.A, lab.B));
    }
}
