using Sholto.App.Analysis.ToolBoundary;

namespace Sholto.App.Analysis.Tests.ToolBoundary;

public class ToolOutcomeFactoryTests
{
    private readonly ToolOutcomeFactory _factory = new();

    [Fact]
    public void Success_carries_value_and_message()
    {
        var outcome = _factory.Success(42, "12 sections");

        Assert.True(outcome.IsSuccess);
        Assert.Equal(42, outcome.Value);
        Assert.Equal("12 sections", outcome.Message);
        Assert.Null(outcome.Reason);
    }

    [Fact]
    public void Success_without_message_leaves_Message_null()
    {
        var outcome = _factory.Success("value");

        Assert.True(outcome.IsSuccess);
        Assert.Null(outcome.Message);
    }

    [Fact]
    public void Failure_of_string_is_unambiguous_and_carries_the_reason()
    {
        var outcome = _factory.Failure<string>("why");

        Assert.False(outcome.IsSuccess);
        Assert.Equal("why", outcome.Reason);
        Assert.Null(outcome.Value);
    }
}
