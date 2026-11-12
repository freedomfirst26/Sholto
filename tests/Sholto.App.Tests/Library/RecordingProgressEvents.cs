using Sholto.Data;

namespace Sholto.App.Tests.Library;

/// <summary>Keeps every <see cref="LibraryAnalysisProgressChanged"/> it is published, and completes
/// <see cref="Finished"/> with the first event whose Running is false.</summary>
internal sealed class RecordingProgressEvents : IEventPublisher
{
    public List<LibraryAnalysisProgressChanged> Events { get; } = [];

    public TaskCompletionSource<LibraryAnalysisProgressChanged> Finished { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Publish<T>(in T e) where T : struct, IEvent
    {
        if (e is not LibraryAnalysisProgressChanged progress) return;
        lock (Events) Events.Add(progress);
        if (!progress.Running) Finished.TrySetResult(progress);
    }
}
