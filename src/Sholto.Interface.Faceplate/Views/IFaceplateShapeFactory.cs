using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;

namespace Sholto.Interface.Faceplate.Views;

public interface IFaceplateShapeFactory
{
    /// <summary>The halo, bloom, tint and outline for the drawing's <paramref name="shape"/> sitting at
    /// <paramref name="rect"/> on the stage. None is added to a canvas; the caller does that, in order.</summary>
    FaceplatePiece CreatePiece(Control shape, Rect rect, bool implemented);

    /// <summary>The transparent shape that answers the pointer for a control, or for a legend speaking for it.</summary>
    Shape CreateHit(Control shape, Rect rect, bool implemented);
}
