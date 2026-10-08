using Sholto.Data;

namespace Sholto.Host.Tests;

public class HintCountQueryTests
{
    // The faceplate's hint policy asks while the window is built, before InitializeServices builds the input
    // stack. Unanswered, it reads 0 every launch and the "first 3 dismissals ever" hint shows forever.
    [Fact]
    public void The_hint_count_is_answered_as_soon_as_the_app_is_composed()
    {
        var app = new AppStackFactory().Create();

        var answer = ((IQueryAsker)app.Sender).Ask<GetHintShownCount, Task<int>>(new GetHintShownCount("faceplate"));

        Assert.NotNull(answer);
    }
}
