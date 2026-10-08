using Avalonia.Controls;
using Avalonia.LogicalTree;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Chips;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.ViewModels.Glance;
using Sholto.Interface.MainUI.Views;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The Track List strip as a control: it builds from its own XAML and follows its view model's empty state.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public class TrackListHeaderXamlTests
{
    public TrackListHeaderXamlTests() => AvaloniaTestApp.EnsureStarted();

    [Fact]
    public void The_header_builds_with_its_chips_and_count()
    {
        var header = new TrackListHeader();

        Assert.NotNull(header.FindControl<ItemsControl>("SourceChips"));
        Assert.NotNull(header.FindControl<TextBlock>("CountText"));
    }

    [Fact]
    public void The_header_has_no_TRACK_LIST_label()
    {
        var header = new TrackListHeader();

        var texts = header.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text);
        Assert.DoesNotContain("TRACK LIST", texts);
    }

    [Fact]
    public void The_strip_and_search_build_their_chips_from_the_same_SourceChip_control()
    {
        var header = new TrackListHeader();
        var overlay = new SearchOverlay();

        var stripChip = Build(header.FindControl<ItemsControl>("SourceChips")!,
            new TrackListSourceChip(new TrackListSource("tag:dnb", TrackListSourceKind.Tag, "Drum And Bass", 16)));
        var searchChip = Build(overlay.FindControl<ItemsControl>("ChipList")!,
            new GlanceChip(GlanceChipKind.Tag, 0, "Drum And Bass"));

        Assert.Single(stripChip);
        Assert.Single(searchChip);
    }

    private List<SourceChip> Build(ItemsControl list, object item)
    {
        var built = list.ItemTemplate!.Build(item)!;
        return [.. new[] { built }.Concat(built.GetLogicalDescendants().OfType<Control>()).OfType<SourceChip>()];
    }
}
