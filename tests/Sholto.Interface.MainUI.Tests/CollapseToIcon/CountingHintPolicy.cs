using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.Tests.CollapseToIcon;

/// <summary>A hint policy that answers as told and counts how often it is asked and told.</summary>
internal sealed class CountingHintPolicy(bool hint) : IHintPolicy
{
    private readonly bool _hint = hint;

    public int Asked { get; private set; }

    public int Shown { get; private set; }

    public bool ShouldHint()
    {
        Asked++;
        return _hint;
    }

    public void HintShown() => Shown++;
}
