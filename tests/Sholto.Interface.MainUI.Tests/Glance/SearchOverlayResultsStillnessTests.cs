using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>Typing replaces the results on every keystroke, so the table must not move while it settles in: no row
/// shift, and the fit bars keep their full height (a regrow from a sliver reads as a jump).</summary>
[Collection(AvaloniaXamlCollection.Name)]
public class SearchOverlayResultsStillnessTests
{
    public SearchOverlayResultsStillnessTests() => AvaloniaTestApp.EnsureStarted();

    [Fact]
    public void A_new_result_does_not_move_the_rows_or_resize_the_fit_bars_at_any_frame()
    {
        var rig = new SearchOverlayRig();
        rig.Rig.Show(GlanceRig.Track("Alpha"), GlanceRig.Track("Bravo"), GlanceRig.Track("Alpine"), GlanceRig.Track("Charlie"));
        rig.Rig.Glance.Open();
        rig.Overlay.UpdateLayout();
        for (var i = 0; i < 20; i++) Step();
        var list = rig.Overlay.FindControl<ListBox>("ResultsList")!;
        var restY = RowTop(list);
        var restHeight = BarHeight(list);

        rig.Rig.Glance.Query = "al";
        rig.Rig.Tick();

        for (var frame = 0; frame < 20; frame++)
        {
            Step();
            Assert.Equal(restY, RowTop(list));
            Assert.Equal(restHeight, BarHeight(list));
        }

        void Step()
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(16);
        }
    }

    /// <summary>Where the first row's top edge is drawn, in overlay coordinates, transforms included.</summary>
    private static double RowTop(ListBox list)
    {
        var row = list.GetVisualDescendants().OfType<ListBoxItem>().First();
        var root = (Visual)list.GetVisualRoot()!;
        return row.TranslatePoint(new Point(0, 0), root)!.Value.Y;
    }

    /// <summary>The first fit bar's drawn height: its layout height times its vertical scale.</summary>
    private static double BarHeight(ListBox list)
    {
        var bar = list.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("fitbar"));
        return bar.Bounds.Height * (bar.RenderTransform?.Value.M22 ?? 1);
    }
}
