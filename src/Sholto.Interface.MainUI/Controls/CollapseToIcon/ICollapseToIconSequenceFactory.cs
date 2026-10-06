namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

public interface ICollapseToIconSequenceFactory
{
    /// <summary>A sequence ticking on the app's frame clock.</summary>
    ICollapseToIconSequence Create(CollapseToIconOptions options, IHintPolicy hintPolicy);
}
