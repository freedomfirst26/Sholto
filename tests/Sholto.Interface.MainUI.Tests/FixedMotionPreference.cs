using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>A motion preference a test fixes by hand.</summary>
internal sealed class FixedMotionPreference(bool reduced) : IMotionPreference
{
    public bool Reduced { get; } = reduced;
}
