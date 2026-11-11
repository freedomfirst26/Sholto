using Avalonia.Controls;
using Sholto.Interface.Faceplate.Devices.DdjFlx4;
using Sholto.Interface.Faceplate.Model;
using Sholto.Interface.Faceplate.ViewModels;
using Sholto.Interface.Faceplate.Views;

namespace Sholto.Interface.Faceplate.Tests;

/// <summary>A panel context over the real doc and an unattached host, for the panel factory tests.</summary>
internal sealed class FaceplateTestRig
{
    public FaceplateTestRig()
    {
        var doc = new FaceplateDocLoader().Load(new DdjFlx4Faceplate());
        ViewModel = new FaceplateViewModel(doc, new RecordingCommandSender());
        var brushes = new FaceplateBrushes();
        var board = new FaceplateBoard(new Grid(), new ContentControl(), new Canvas(),
            _ => true, () => 0, (_, _) => { }, _ => false, brushes,
            new FaceplateShapeFactory(brushes), new FaceplateStepBadgeFactory(brushes));
        Context = new FaceplatePanelContext(new Border(), ViewModel, board);
        Parts = new FaceplatePanelPartsFactory(new ProseInlinesFactory());
    }

    public FaceplateViewModel ViewModel { get; }
    public FaceplatePanelContext Context { get; }
    public IFaceplatePanelPartsFactory Parts { get; }

    public static GestureRow Row(string id, string layer = "plain", string result = "Does a thing.",
                                 bool used = true, string verb = "press", string? part = null,
                                 params string[] partners) =>
        new(id, "transport.play.0", verb, part, layer, result, used, partners);
}
