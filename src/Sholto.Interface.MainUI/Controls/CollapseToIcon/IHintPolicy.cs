namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>Decides whether a finished collapse is followed by the pulsing hint.</summary>
public interface IHintPolicy
{
    bool ShouldHint();

    /// <summary>The hint is starting; called once per hint.</summary>
    void HintShown();
}
