namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>Where a collapse-to-icon sequence is. <see cref="Open"/> and <see cref="Collapsing"/> show the
/// container; <see cref="Hinting"/> and <see cref="Idle"/> have it hidden in its icon.</summary>
public enum CollapseToIconState
{
    Open,
    Collapsing,
    Hinting,
    Idle,
}
