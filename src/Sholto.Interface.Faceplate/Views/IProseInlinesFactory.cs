using Avalonia.Controls.Documents;
using Sholto.Interface.Faceplate.Model;

namespace Sholto.Interface.Faceplate.Views;

/// <summary>Builds the inlines (text runs and control chips) for a prose string.</summary>
public interface IProseInlinesFactory
{
    InlineCollection Create(
        string text,
        FaceplateDoc doc,
        int deck,
        Action<string, int>? onHover = null,
        Action<string, int>? onHoverEnd = null,
        Action<string, int>? onActivate = null);
}
