using Sholto.App.Audio.Recording;

namespace Sholto.App.Tests.Recording;

/// <summary>Hands out <c>take-1.wav</c>, <c>take-2.wav</c>, …, or throws <see cref="Failure"/> when it is set.</summary>
internal sealed class FakeMixRecordingPaths : IMixRecordingPaths
{
    private int _taken;

    public Exception? Failure { get; set; }

    public DateTime? LastLocalNow { get; private set; }

    public string Next(DateTime localNow)
    {
        if (Failure is { } failure) throw failure;
        LastLocalNow = localNow;
        return $"take-{++_taken}.wav";
    }
}
