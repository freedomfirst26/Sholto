using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.Tests.CollapseToIcon;

public class FirstDismissalsHintPolicyTests
{
    [Fact]
    public void Hints_three_times_then_stops()
    {
        var counter = new FakeHintCounter();
        var policy = new FirstDismissalsHintPolicy(3, counter);

        for (var i = 0; i < 3; i++)
        {
            Assert.True(policy.ShouldHint());
            policy.HintShown();
        }

        Assert.False(policy.ShouldHint());
        Assert.Equal(3, counter.Recorded);
    }

    [Fact]
    public async Task A_saved_count_at_the_limit_means_no_hint()
    {
        var policy = new FirstDismissalsHintPolicy(3, new FakeHintCounter(saved: 3));
        await Task.Yield();

        Assert.False(policy.ShouldHint());
    }

    [Fact]
    public async Task A_saved_count_below_the_limit_leaves_the_rest()
    {
        var policy = new FirstDismissalsHintPolicy(3, new FakeHintCounter(saved: 2));
        await Task.Yield();

        Assert.True(policy.ShouldHint());
        policy.HintShown();
        Assert.False(policy.ShouldHint());
    }

    [Fact]
    public void Hints_before_the_saved_count_arrives()
    {
        var policy = new FirstDismissalsHintPolicy(3, new FakeHintCounter(saved: 3, answerAtOnce: false));

        Assert.True(policy.ShouldHint());
    }

    [Fact]
    public void Two_policies_over_two_counters_are_independent()
    {
        var a = new FirstDismissalsHintPolicy(3, new FakeHintCounter());
        var b = new FirstDismissalsHintPolicy(3, new FakeHintCounter());

        for (var i = 0; i < 3; i++) a.HintShown();

        Assert.False(a.ShouldHint());
        Assert.True(b.ShouldHint());
    }
}
