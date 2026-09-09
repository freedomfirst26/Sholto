using System.Xml.Linq;
using Sholto.Controller.Gestures;
using Sholto.Faceplate.Model;
using Xunit;

namespace Sholto.Faceplate.Tests;

public class FaceplateDocTests
{
    private static FaceplateDoc Flx4() => FaceplateDocLoader.LoadEmbedded("ddj-flx4");

    [Fact]
    public void The_shipped_file_loads()
    {
        var doc = Flx4();
        Assert.Equal("DDJ-FLX4", doc.Device);
        Assert.NotEmpty(doc.Controls);
    }

    [Fact]
    public void Every_gesture_the_recognizer_can_emit_has_a_description()
    {
        var described = Flx4().Controls.SelectMany(c => c.Gestures).Select(g => g.Id).ToHashSet();
        var missing = GestureIds.All.Where(id => !described.Contains(id)).ToList();
        Assert.True(missing.Count == 0,
            "Gestures with no description in ddj-flx4.guide.json: " + string.Join(", ", missing));
    }

    [Fact]
    public void No_described_gesture_is_invented()
    {
        // A described id the recognizer cannot emit is a description nobody will read.
        var known = GestureIds.All.ToHashSet();
        var invented = Flx4().Controls.SelectMany(c => c.Gestures)
                             .Select(g => g.Id).Where(id => !known.Contains(id)).ToList();
        Assert.True(invented.Count == 0,
            "Described gestures the recognizer cannot emit: " + string.Join(", ", invented));
    }

    [Fact]
    public void Every_partner_named_by_with_is_a_real_control()
    {
        var doc = Flx4();
        var controlIds = doc.Controls.Select(c => c.Id).ToHashSet();
        foreach (var c in doc.Controls)
            foreach (var g in c.Gestures)
                foreach (var partner in g.With ?? [])
                    Assert.True(controlIds.Contains(partner),
                        $"Gesture '{g.Id}' names partner '{partner}', which is not a control.");
    }

    [Fact]
    public void Every_gesture_names_a_real_layer()
    {
        var doc = Flx4();
        var layerIds = doc.Layers.Select(l => l.Id).ToHashSet();
        foreach (var c in doc.Controls)
            foreach (var g in c.Gestures)
                Assert.True(layerIds.Contains(g.Layer),
                    $"Gesture '{g.Id}' names layer '{g.Layer}', which is not declared.");
    }

    [Fact]
    public void Every_layers_modifier_is_a_real_control()
    {
        var doc = Flx4();
        var controlIds = doc.Controls.Select(c => c.Id).ToHashSet();
        foreach (var l in doc.Layers.Where(l => l.Modifier is not null))
            Assert.True(controlIds.Contains(l.Modifier!),
                $"Layer '{l.Id}' names modifier '{l.Modifier}', which is not a control.");
    }

    [Fact]
    public void Every_control_in_the_data_file_has_a_shape_in_the_layout()
    {
        // Reads the .axaml as plain XML, so this needs no Avalonia runtime.
        var shapeIds = LayoutIds();
        foreach (var c in Flx4().Controls)
            Assert.True(shapeIds.Contains(c.Id),
                $"Control '{c.Id}' is described but has no shape in DdjFlx4Layout.axaml.");
    }

    [Fact]
    public void Every_shape_in_the_layout_is_described()
    {
        var described = Flx4().Controls.Select(c => c.Id).ToHashSet();
        foreach (var id in LayoutIds())
            Assert.True(described.Contains(id),
                $"Shape '{id}' is drawn but has no entry in ddj-flx4.guide.json.");
    }

    [Fact]
    public void Every_control_carries_a_summary_and_every_gesture_a_result()
    {
        foreach (var c in Flx4().Controls)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Label), $"Control '{c.Id}' has no label.");
            Assert.False(string.IsNullOrWhiteSpace(c.Summary), $"Control '{c.Id}' has no summary.");
            foreach (var g in c.Gestures)
            {
                Assert.False(string.IsNullOrWhiteSpace(g.Verb), $"Gesture '{g.Id}' has no verb.");
                Assert.False(string.IsNullOrWhiteSpace(g.Result), $"Gesture '{g.Id}' has no result.");
            }
        }
    }

    /// <summary>Every ControlSurface.Id in the layout XAML, read as plain XML.</summary>
    private static HashSet<string> LayoutIds()
    {
        var path = Path.Combine(RepoRoot(), "src/Sholto.Faceplate/Devices/DdjFlx4/DdjFlx4Layout.axaml");
        var doc = XDocument.Load(path);
        return doc.Descendants()
                  .SelectMany(e => e.Attributes())
                  .Where(a => a.Name.LocalName == "ControlSurface.Id")
                  .Select(a => a.Value)
                  .ToHashSet();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sholto.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Sholto.slnx not found above the test binary.");
    }
}
