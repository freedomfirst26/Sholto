using Avalonia.Controls;
using Avalonia.Media;
using Sholto.Interface.Faceplate.Views;

namespace Sholto.Interface.Faceplate.Tests;

public class FaceplateHeadlineFactoryTests
{
    private readonly FaceplateTestRig _rig = new();
    private readonly FaceplateHeadlineFactory _factory;

    public FaceplateHeadlineFactoryTests() => _factory = new FaceplateHeadlineFactory(_rig.Parts);

    private static TextBlock Answer(FaceplatePanelSection section) =>
        Assert.IsType<TextBlock>(((Grid)section.Children[1]).Children[1]);

    [Fact]
    public void A_used_headline_builds_eyebrow_and_answer_with_one_hidden_indicator()
    {
        var section = _factory.Create(FaceplateTestRig.Row("g1"), _rig.Context);

        Assert.Equal(2, section.Children.Count);
        Assert.Equal("IF YOU JUST PRESS IT", Assert.IsType<TextBlock>(section.Children[0]).Text);
        var indicator = Assert.Single(section.Indicators);
        Assert.Equal("g1", indicator.Key);
        Assert.Equal(0, indicator.Value.Opacity);
    }

    [Fact]
    public void An_unused_headline_reads_italic_at_0_62_and_keeps_16px()
    {
        var section = _factory.Create(FaceplateTestRig.Row("g1", used: false), _rig.Context);

        var answer = Answer(section);
        Assert.Equal(FontStyle.Italic, answer.FontStyle);
        Assert.Equal(FontWeight.Normal, answer.FontWeight);
        Assert.Equal(0.62, answer.Opacity);
        Assert.Equal(16, answer.FontSize);
    }

    [Fact]
    public void Prose_with_a_chip_gets_the_24px_line_and_plain_prose_keeps_22()
    {
        var chipped = _factory.Create(FaceplateTestRig.Row("g1", result: "Hold [[deck.shift]] to unlock it."), _rig.Context);
        var plain = _factory.Create(FaceplateTestRig.Row("g2"), _rig.Context);

        Assert.Equal(24, Answer(chipped).LineHeight);
        Assert.Equal(22, Answer(plain).LineHeight);
    }
}
