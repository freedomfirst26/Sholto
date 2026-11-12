using Xunit;

namespace Sholto.App.Audio.Tests;

/// <summary>Off Linux the router must never start <c>pw-link</c>/<c>pactl</c>. With
/// isLinux false every method returns its empty result; on a Linux box with pw-link
/// and pactl installed these results would differ (and the card would be consulted),
/// so the test proves the guard rather than the tools' absence.</summary>
public sealed class PipeWireRouterOffLinuxTests
{
    private readonly PipeWireRouter _router = new(new PipeWireOwnPorts(), Environment.ProcessId, isLinux: false);
    private readonly RecordingCard _card = new();

    [Fact]
    public void IsAvailable_is_false() => Assert.False(_router.IsAvailable());

    [Fact]
    public void EnumerateSpeakerSinks_is_empty_and_never_consults_the_card()
    {
        Assert.Empty(_router.EnumerateSpeakerSinks(_card));
        Assert.Equal(0, _card.Calls);
    }

    [Fact]
    public void FindControllerSink_is_null_and_never_consults_the_card()
    {
        Assert.Null(_router.FindControllerSink(_card));
        Assert.Equal(0, _card.Calls);
    }

    [Fact]
    public void ApplyMasterRoute_is_false_with_a_reason()
    {
        Assert.False(_router.ApplyMasterRoute("controller", "speaker", out var log));
        Assert.Contains("not on Linux", log);
    }

    [Fact]
    public void ResetMasterRoute_does_nothing_and_does_not_throw()
    {
        _router.ResetMasterRoute("controller", _card);
        Assert.Equal(0, _card.Calls);
    }

    private sealed class RecordingCard : IControllerSoundCard
    {
        public int Calls { get; private set; }

        public bool Matches(string? nodeOrDesc)
        {
            Calls++;
            return false;
        }
    }
}
