using Sholto.Data;

namespace Sholto.Data.Tests;

public sealed class LambdaFrameHandler(Action<DateTime> onFrame) : IFrameTickHandler
{
    private readonly Action<DateTime> _onFrame = onFrame;

    public void OnFrame(DateTime now) => _onFrame(now);
}
