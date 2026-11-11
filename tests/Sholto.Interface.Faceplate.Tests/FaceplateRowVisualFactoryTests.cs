using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Sholto.Interface.Faceplate.Views;

namespace Sholto.Interface.Faceplate.Tests;

public class FaceplateRowVisualFactoryTests
{
    private readonly FaceplateTestRig _rig = new();
    private readonly FaceplateRowVisualFactory _factory;

    public FaceplateRowVisualFactoryTests() =>
        _factory = new FaceplateRowVisualFactory(_rig.Parts, new ComboMarkerFormatter());

    private static StackPanel CardStack(Control indicatedRow) =>
        Assert.IsType<StackPanel>(Assert.IsType<Border>(((Grid)indicatedRow).Children[1]).Child);

    [Fact]
    public void No_rows_build_an_empty_section()
    {
        var section = _factory.Create([], _rig.Context);

        Assert.Empty(section.Children);
        Assert.Empty(section.Indicators);
    }

    [Fact]
    public void Two_rows_build_label_row_rule_row_with_one_hidden_indicator_each()
    {
        var section = _factory.Create([FaceplateTestRig.Row("a"), FaceplateTestRig.Row("b")], _rig.Context);

        Assert.Equal(4, section.Children.Count);
        Assert.Equal("EVERYTHING ELSE IT DOES", Assert.IsType<TextBlock>(section.Children[0]).Text);
        Assert.Equal(1, Assert.IsType<Border>(section.Children[2]).Height);
        Assert.Equal(["a", "b"], section.Indicators.Keys.Order());
        Assert.All(section.Indicators.Values, i => Assert.Equal(0, i.Opacity));
    }

    [Fact]
    public void A_shift_layer_row_without_shift_in_its_chord_carries_a_SHIFT_tag()
    {
        var section = _factory.Create([FaceplateTestRig.Row("a", layer: "shift")], _rig.Context);

        var stack = CardStack(section.Children[1]);
        Assert.Equal(3, stack.Children.Count);
        var tag = Assert.IsType<Border>(stack.Children[0]);
        var expected = _rig.ViewModel.Doc.Layers.First(l => l.Id == "shift").Name.ToUpperInvariant();
        Assert.Equal(expected, Assert.IsType<TextBlock>(tag.Child).Text);
    }

    [Fact]
    public void A_plain_layer_row_carries_no_tag()
    {
        var section = _factory.Create([FaceplateTestRig.Row("a")], _rig.Context);

        Assert.Equal(2, CardStack(section.Children[1]).Children.Count);
    }

    [Fact]
    public void An_unused_row_reads_italic_at_0_70()
    {
        var section = _factory.Create([FaceplateTestRig.Row("a", used: false)], _rig.Context);

        var result = Assert.IsType<TextBlock>(CardStack(section.Children[1]).Children[^1]);
        Assert.Equal(FontStyle.Italic, result.FontStyle);
        Assert.Equal(0.70, result.Opacity);
    }

    [Fact]
    public void A_global_selection_points_row_chips_at_deck_0()
    {
        Assert.Equal(-1, _rig.ViewModel.SelectedDeck);

        var section = _factory.Create(
            [FaceplateTestRig.Row("a", result: "Hold [[deck.shift]] to unlock it.")], _rig.Context);

        var result = Assert.IsType<TextBlock>(CardStack(section.Children[1]).Children[^1]);
        var chip = Assert.IsType<ControlChip>(result.Inlines!.OfType<InlineUIContainer>().Single().Child);
        Assert.Equal(0, chip.Deck);
    }
}
