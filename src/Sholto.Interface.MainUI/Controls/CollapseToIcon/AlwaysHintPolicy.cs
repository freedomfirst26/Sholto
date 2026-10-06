namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>Every collapse is followed by the hint.</summary>
public sealed class AlwaysHintPolicy : IHintPolicy
{
    public bool ShouldHint() => true;

    public void HintShown() { }
}
