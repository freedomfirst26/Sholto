using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.Tests.CollapseToIcon;

/// <summary>A hint counter held in memory; the saved count is answered when the test completes it.</summary>
internal sealed class FakeHintCounter(int saved = 0, bool answerAtOnce = true) : IHintCounter
{
    private readonly TaskCompletionSource<int> _answer = new();

    public int Recorded { get; private set; }

    public Task<int> CountAsync()
    {
        if (answerAtOnce) _answer.TrySetResult(saved);
        return _answer.Task;
    }

    public void Record() => Recorded++;

    public void Answer() => _answer.TrySetResult(saved);
}
