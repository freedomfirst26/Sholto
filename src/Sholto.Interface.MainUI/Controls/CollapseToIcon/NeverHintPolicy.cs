namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>No collapse is followed by the hint: the shrink alone is the cue.</summary>
public sealed class NeverHintPolicy : IHintPolicy
{
    public bool ShouldHint() => false;

    public void HintShown() { }
}
