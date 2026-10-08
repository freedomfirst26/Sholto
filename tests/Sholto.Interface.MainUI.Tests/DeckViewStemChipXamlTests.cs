using Avalonia.Controls;
using Avalonia.LogicalTree;
using Sholto.Data;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.Views;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The deck's stem chips as XAML: each chip's opacity is bound to its view model, so a turned-down
/// stem dims.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public class DeckViewStemChipXamlTests
{
    public DeckViewStemChipXamlTests() => AvaloniaTestApp.EnsureStarted();

    [Theory]
    [InlineData("DRMS", 0)]
    [InlineData("VOX", 1)]
    [InlineData("INST", 2)]
    public void Each_stem_chip_opacity_follows_its_stem_level(string label, int stem)
    {
        var bus = new DataBus(new ThrowingFailureSink());
        var deck = new DeckViewModel(0, bus, bus, new ThemeStackFactory().Build().Context, new NoPeaksFactory(),
            new DiscBloomFactory(new FakeFrameClock()));
        var view = new DeckView { DataContext = deck };
        var chip = view.GetLogicalDescendants().OfType<TextBlock>()
            .First(t => t.Text == label).GetLogicalParent() as Grid;
        Assert.NotNull(chip);
        Assert.Equal(1.0, chip.Opacity);

        bus.Publish(new StemLevelChanged(0, stem, 0.0));

        Assert.Equal(0.3, chip.Opacity, 6);
    }
}
