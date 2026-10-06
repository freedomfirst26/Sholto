using Avalonia.Controls;
using Sholto.Interface.MainUI.Views;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>The Glance overlay as a control: built from its own XAML, with the tag-completion ghost and the Tab hint present.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public class SearchOverlayXamlTests
{
    public SearchOverlayXamlTests() => AvaloniaTestApp.EnsureStarted();

    [Fact]
    public void The_overlay_builds_with_its_completion_ghost_and_tab_hint()
    {
        var overlay = new SearchOverlay();
        Assert.NotNull(overlay.FindControl<TextBlock>("CompletionGhost"));
        Assert.NotNull(overlay.FindControl<TextBlock>("TabHintText"));
    }
}
