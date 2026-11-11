using Sholto.Interface.Faceplate.ViewModels;

namespace Sholto.Interface.Faceplate.Views;

public interface IFaceplateRowVisualFactory
{
    /// <summary>The "EVERYTHING ELSE IT DOES" list: label, hairlines and one card per row.
    /// Empty rows give a section with no children and no indicators.</summary>
    FaceplatePanelSection Create(IReadOnlyList<GestureRow> rows, FaceplatePanelContext context);
}
