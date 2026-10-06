namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>The desktop's own "animations on" setting.</summary>
public interface IDesktopAnimationSetting
{
    /// <summary>"true" or "false" as the desktop reports it; null when it cannot be read.</summary>
    string? Read();
}
