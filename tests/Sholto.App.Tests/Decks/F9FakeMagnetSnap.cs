using Sholto.App.Performance;

namespace Sholto.App.Tests;

/// <summary>A magnet snap that does nothing but let the test raise its eligibility event.</summary>
internal sealed class F9FakeMagnetSnap : IMagnetSnap
{
    public event Action<bool>? EligibilityChanged;

    public void Raise(bool eligible) => EligibilityChanged?.Invoke(eligible);

    public double Factor() => 1.0;

    public void Update()
    {
    }
}
