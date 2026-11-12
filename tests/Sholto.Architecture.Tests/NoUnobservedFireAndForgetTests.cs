namespace Sholto.Architecture.Tests;

/// <summary>A <c>_ = XAsync(...)</c> call drops the task, so an exception in XAsync is never observed. Every unqualified
/// call to a same-file async method must target a method with a top-level try/catch, or be in <see cref="Allowed"/>.</summary>
public sealed class NoUnobservedFireAndForgetTests
{
    // FROZEN: today's real hits as "FileStem.Method", sorted. It may only shrink: remove an entry when its method is
    // guarded, renamed or deleted (the stale-entry test fails until you do). Never add one.
    private static readonly string[] Allowed =
    [
        "CratePickerViewModel.RefreshAsync",
        "DeckMarkers.LoadForDeckAsync",
        "LibrarySession.FileIntoAllTracksAsync",
        "MidiManager.SuperviseAsync",
        "OutputDeviceChooser.ChangeOutputDeviceAsync",
        "TagEditorViewModel.RefreshSuggestionsAsync",
        "ToastViewModel.HideLaterAsync",
        "TrackActionsCoordinator.OpenCratePickerAsync",
        "TrackActionsCoordinator.OpenTagEditorAsync",
    ];

    private readonly SourceFiles _sources = new();
    private readonly UnguardedFireAndForgetScanner _scanner = new();

    [Fact]
    public void PlantedUnguardedFireAndForgetIsFound() =>
        Assert.Equal(["FooAsync"], Scan("void Go() => _ = FooAsync();\nprivate async Task FooAsync() { await Task.Delay(1); }"));

    [Fact]
    public void PlantedGuardedFireAndForgetIsNotFound() =>
        Assert.Empty(Scan("void Go() => _ = FooAsync();\nprivate async Task FooAsync() { try { await Task.Delay(1); } catch (Exception) { } }"));

    [Fact]
    public void TryNestedInALoopDoesNotCountAsGuarded() =>
        Assert.Equal(["FooAsync"], Scan("void Go() => _ = FooAsync();\nprivate async Task FooAsync() { while (true) { try { await Task.Delay(1); } catch { break; } } }"));

    [Fact]
    public void ExpressionBodiedAsyncIsUnguarded() =>
        Assert.Equal(["FooAsync"], Scan("void Go() => _ = FooAsync();\nprivate async Task FooAsync() => await Task.Delay(1);"));

    [Fact]
    public void TryFinallyWithoutCatchIsUnguarded() =>
        Assert.Equal(["FooAsync"], Scan("void Go() => _ = FooAsync();\nprivate async Task FooAsync() { try { await Task.Delay(1); } finally { } }"));

    [Fact]
    public void QualifiedAndTaskRunCallsAreIgnored() =>
        Assert.Empty(Scan("void Go() { _ = _store.PutAsync(1); _ = Task.Run(() => 1); }\nprivate async Task PutAsync(int x) { await Task.Delay(1); }"));

    [Fact]
    public void BracesInInterpolatedStringsDoNotEndTheBody() =>
        Assert.Empty(Scan("void Go() => _ = FooAsync();\nprivate async Task FooAsync() { var s = $\"{1}\"; try { await Task.Delay(1); } catch { } }"));

    [Fact]
    public void CommentMentioningTryAndCatchDoesNotCountAsGuarded() =>
        Assert.Equal(["FooAsync"], Scan("void Go() => _ = FooAsync();\nprivate async Task FooAsync() { // try again, then catch it\n await Task.Delay(1); }"));

    [Fact]
    public void UnclosedBodyIsUnguardedAndDoesNotThrow() =>
        Assert.Equal(["FooAsync"], Scan("void Go() => _ = FooAsync();\nprivate async Task FooAsync() { var c = '{'; await Task.Delay(1);"));

    [Fact]
    public void NoUnguardedFireAndForgetOutsideTheAllowlist()
    {
        var offenders = Found().Where(hit => !Allowed.Contains(hit.Key)).Select(hit => hit.Description).ToList();

        Assert.True(offenders.Count == 0,
            "A `_ = XAsync(...)` call drops the task; give XAsync a top-level try/catch that logs, or await it. These are unguarded:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryAllowlistEntryStillExists()
    {
        var found = Found().Select(hit => hit.Key).ToHashSet();
        var stale = Allowed.Where(entry => !found.Contains(entry)).ToList();

        Assert.True(stale.Count == 0,
            "Allowlist entries are no longer offenders; remove them so the list only shrinks:\n" + string.Join("\n", stale));
    }

    private IReadOnlyList<string> Scan(string text) => _scanner.UnguardedTargets(text);

    private List<(string Key, string Description)> Found() =>
        _sources.All()
            .SelectMany(path => _scanner.UnguardedTargets(File.ReadAllText(path))
                .Select(method =>
                {
                    var key = Path.GetFileNameWithoutExtension(path) + "." + method;
                    return (Key: key, Description: $"{key} ({Path.GetRelativePath(_sources.RepoRoot(), path)})");
                }))
            .ToList();
}
