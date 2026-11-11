using Sholto.Interface.Faceplate.ViewModels;

namespace Sholto.Interface.Faceplate.Views;

public interface IFaceplateHeadlineFactory
{
    /// <summary>Two children (eyebrow, indicated answer line); exactly one indicator, keyed by <c>headline.GestureId</c>.</summary>
    FaceplatePanelSection Create(GestureRow headline, FaceplatePanelContext context);
}
