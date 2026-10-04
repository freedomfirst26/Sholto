using Sholto.App.Storage;
using Sholto.App.Library;
using Sholto.App.Lifecycle;

namespace Sholto.App.Tests;

/// <summary>The library database as the lifecycle sees it. With a stack it "opens" to it (after
/// <see cref="Release"/>, if it was built gated); with null it is the unavailable database: it never
/// runs the open callback and <see cref="Opened"/> completes with null, exactly as the real one does when
/// opening fails.</summary>
internal sealed class FakeLibraryDatabase(DatabaseStack? stack, bool gated = false) : ILibraryDatabase
{
    private readonly DatabaseStack? _stack = stack;
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<DatabaseStack?> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly bool _gated = gated;

    public Task<DatabaseStack?> Opened => _ready.Task;

    public LibraryStack? Stores =>
        _stack is null ? null : new LibraryStack(_stack.Tracks, _stack.Tags, _stack.BasicAnalyses, _stack.TempoMultipliers);

    /// <summary>Let a gated database finish "opening".</summary>
    public void Release() => _gate.TrySetResult();

    public async Task OpenAsync(Func<DatabaseStack, Task> onOpened)
    {
        try
        {
            if (_gated) await _gate.Task;
            if (_stack is not null) await onOpened(_stack);
        }
        finally
        {
            _ready.TrySetResult(_stack);
        }
    }
}
