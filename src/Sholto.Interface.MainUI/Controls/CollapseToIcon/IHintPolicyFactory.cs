namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

public interface IHintPolicyFactory
{
    IHintPolicy Always();

    IHintPolicy Never();

    /// <summary>Hints for the first <paramref name="count"/> dismissals of the consumer named
    /// <paramref name="key"/> (lowercase letters and underscores), counted across launches.</summary>
    IHintPolicy FirstDismissals(string key, int count);
}
