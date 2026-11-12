using Sholto.App.Analysis.Stems;
using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Lifecycle.Folders;

/// <inheritdoc />
/// <remarks>The rules, per row, at startup:
/// <list type="bullet">
/// <item>Music: <c>SHOLTO_MUSIC_DIR</c> is Fixed; a saved folder that exists is Ok; one that is gone is Missing
/// (and the library is told it is unreachable); none saved is NotChosen.</item>
/// <item>Analysis: <c>SHOLTO_CACHE_ROOT</c> is Fixed and never saved; a saved folder that exists is Ok; one that
/// is gone is Missing and is never created; none saved is NotChosen, pre-filled with the legacy root on an
/// existing install (an upgrade) and with <see cref="IFolderCheck.SuggestFor"/> otherwise.</item>
/// </list>
/// When every row is Ok or Fixed nothing is asked. Otherwise one <see cref="FoldersNeeded"/> shows both rows.
/// Cancelling keeps what is saved and leaves the analysis folder unset, so stems stay off this session.</remarks>
public sealed class LibraryFolders(
    ISettingPreference musicDirPreference,
    ISettingPreference analysisDirPreference,
    IAnalysisFolder folder,
    IStemGate gate,
    IStemFolderMover mover,
    IFolderCheck check,
    ILibrarySession library,
    ILibraryDatabase database,
    IAppThread appThread,
    IEventPublisher publisher,
    string? musicOverride,
    string? analysisOverride,
    string legacyAnalysisRoot,
    ILog libraryLog,
    ILog foldersLog) : ILibraryFolders
{
    private readonly ISettingPreference _musicDirPreference = musicDirPreference;
    private readonly ISettingPreference _analysisDirPreference = analysisDirPreference;
    private readonly IAnalysisFolder _folder = folder;
    private readonly IStemGate _gate = gate;
    private readonly IStemFolderMover _mover = mover;
    private readonly IFolderCheck _check = check;
    private readonly ILibrarySession _library = library;
    private readonly ILibraryDatabase _database = database;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;
    // The SHOLTO_MUSIC_DIR and SHOLTO_CACHE_ROOT environment overrides, read once by the composition root.
    private readonly string? _musicOverride = string.IsNullOrWhiteSpace(musicOverride) ? null : musicOverride;
    private readonly string? _analysisOverride = string.IsNullOrWhiteSpace(analysisOverride) ? null : analysisOverride;
    private readonly string _legacyAnalysisRoot = legacyAnalysisRoot;
    private readonly ILog _libraryLog = libraryLog;
    private readonly ILog _foldersLog = foldersLog;

    // The question currently put to the user, if any: completed by the matching ChooseFolders command.
    private TaskCompletionSource<ChooseFolders>? _answer;
    // True while a menu-initiated question is being worked through (set on the app thread, so a second request is dropped).
    private bool _changing;

    /// <summary>Both rows as they stand, with what the question needs to apply an answer.</summary>
    /// <param name="OldAnalysisRoot">Where the stems are now (the root a change moves them from); null when there are none to move.</param>
    private sealed record Snapshot(
        FoldersReason Reason, FolderRow Music, FolderRow Analysis, long StemBytes, string? OldAnalysisRoot, bool IsUpgrade);

    /// <summary>What applying one answer ended in: a fresh question when it failed, else the music folder chosen.</summary>
    private sealed record Applied(Snapshot? Retry, string? Music);

    // ---- Startup --------------------------------------------------------------------------------

    public async Task<string?> ResolveAsync()
    {
        var snapshot = await SnapshotAsync(menu: false);
        var effectiveMusic = _musicOverride ?? await _musicDirPreference.GetAsync();
        if (!string.IsNullOrEmpty(effectiveMusic) && !Directory.Exists(effectiveMusic))
        {
            _libraryLog.Warn($"saved music dir not reachable: {effectiveMusic}");
            await _appThread.InvokeAsync(() => _library.SetUnreachablePath(effectiveMusic));
        }

        if (IsSettled(snapshot.Music) && IsSettled(snapshot.Analysis))
        {
            if (snapshot.Analysis.Path is { } root) _folder.Use(root);
            return MusicToScan(snapshot);
        }

        var asked = await AskUntilSettledAsync(snapshot, scanMusic: false);
        return asked.Cancelled ? MusicToScan(snapshot) : asked.Music;
    }

    private bool IsSettled(FolderRow row) => row.State is FolderRowState.Ok or FolderRowState.Fixed;

    // The music folder to scan when the question is not answered: the one in force, if it is there.
    private string? MusicToScan(Snapshot snapshot) =>
        snapshot.Music.State is FolderRowState.Ok or FolderRowState.Fixed
        && snapshot.Music.Path is { } path && Directory.Exists(path) ? path : null;

    // ---- The question ---------------------------------------------------------------------------

    public void Handle(in ChangeFolders command)
    {
        if (_changing || _answer is not null) return;  // already asking
        _changing = true;
        _ = ChangeAsync();
    }

    private async Task ChangeAsync()
    {
        try
        {
            var snapshot = await SnapshotAsync(menu: true);
            await AskUntilSettledAsync(snapshot, scanMusic: true);
        }
        catch (Exception ex) { _foldersLog.Error($"change failed: {ex.Message}"); }
        finally { _changing = false; }
    }

    public void Handle(in ChooseFolders command)
    {
        var answer = _answer;
        if (answer is null) return;
        _answer = null;
        answer.TrySetResult(command);
    }

    private async Task<(bool Cancelled, string? Music)> AskUntilSettledAsync(Snapshot first, bool scanMusic)
    {
        var current = first;
        while (true)
        {
            var answer = await AskAsync(current);
            if (answer.Music is null && answer.Analysis is null) return (true, null);

            var applied = await ApplyAsync(answer, current, scanMusic);
            if (applied.Retry is null) return (false, applied.Music);
            current = applied.Retry;
        }
    }

    private async Task<ChooseFolders> AskAsync(Snapshot snapshot)
    {
        var answer = new TaskCompletionSource<ChooseFolders>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _appThread.InvokeAsync(() =>
        {
            _answer = answer;
            _publisher.Publish(new FoldersNeeded(snapshot.Reason, snapshot.Music, snapshot.Analysis, snapshot.StemBytes));
        });
        return await answer.Task.ConfigureAwait(false);
    }

    public Task<FolderCheckResult> Handle(in CheckFolders query)
    {
        var music = query.Music;
        var analysis = query.Analysis;
        return Task.Run(() =>
        {
            var result = _check.Check(music, analysis);
            // No suggestion from beside the music folder (its parent is / or read-only): offer the legacy root.
            return result.SuggestedAnalysis is null && !string.IsNullOrWhiteSpace(music)
                ? result with { SuggestedAnalysis = _legacyAnalysisRoot }
                : result;
        });
    }

    // ---- Rows -----------------------------------------------------------------------------------

    private async Task<Snapshot> SnapshotAsync(bool menu)
    {
        var savedMusic = Clean(await _musicDirPreference.GetAsync());
        var savedAnalysis = Clean(await _analysisDirPreference.GetAsync());
        var shownMusic = menu ? await _appThread.InvokeAsync(() => _library.CurrentDir) : null;

        var music = MusicRow(shownMusic ?? savedMusic);
        var effectiveMusic = _musicOverride ?? shownMusic ?? savedMusic;

        bool isUpgrade = false;
        string? oldRoot = null;
        FolderRow analysis;
        if (_analysisOverride is not null)
            analysis = new FolderRow(_analysisOverride, FolderRowState.Fixed, null);
        else if (_folder.Root is { } inUse)
        {
            analysis = new FolderRow(inUse, FolderRowState.Ok, null);
            oldRoot = inUse;
        }
        else if (savedAnalysis is not null)
        {
            if (Directory.Exists(savedAnalysis))
            {
                analysis = new FolderRow(savedAnalysis, FolderRowState.Ok, null);
                oldRoot = savedAnalysis;
            }
            else
                analysis = new FolderRow(savedAnalysis, FolderRowState.Missing, "Not found. Is the drive mounted?");
        }
        else if (savedMusic is not null || Directory.Exists(Path.Combine(_legacyAnalysisRoot, "stems")))
        {
            // An existing install that has never chosen: offer to keep where its stems already are.
            isUpgrade = true;
            analysis = new FolderRow(_legacyAnalysisRoot, FolderRowState.NotChosen, null);
            oldRoot = _legacyAnalysisRoot;
        }
        else
        {
            var suggestion = _check.SuggestFor(effectiveMusic) ?? (effectiveMusic is null ? null : _legacyAnalysisRoot);
            analysis = new FolderRow(suggestion, FolderRowState.NotChosen, null);
        }

        var bytes = oldRoot is null ? 0 : await Task.Run(() => StemBytesIn(oldRoot));
        return new Snapshot(ReasonFor(menu, music, analysis, isUpgrade), music, analysis, bytes, oldRoot, isUpgrade);
    }

    private FolderRow MusicRow(string? saved)
    {
        if (_musicOverride is not null)
            return new FolderRow(_musicOverride, FolderRowState.Fixed, Directory.Exists(_musicOverride) ? null : "Not found.");
        if (saved is null) return new FolderRow(null, FolderRowState.NotChosen, null);
        return Directory.Exists(saved)
            ? new FolderRow(saved, FolderRowState.Ok, null)
            : new FolderRow(saved, FolderRowState.Missing, "Not found. Is the drive mounted?");
    }

    private FoldersReason ReasonFor(bool menu, FolderRow music, FolderRow analysis, bool isUpgrade)
    {
        if (menu) return FoldersReason.Change;
        if (music.State == FolderRowState.Missing || analysis.State == FolderRowState.Missing) return FoldersReason.DriveNotFound;
        if (music.State == FolderRowState.NotChosen) return FoldersReason.FirstRun;
        return isUpgrade ? FoldersReason.Upgrade : FoldersReason.FirstRun;
    }

    private long StemBytesIn(string root)
    {
        var stems = Path.Combine(root, "stems");
        if (!Directory.Exists(stems)) return 0;
        try { return new DirectoryInfo(stems).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 0; }
    }

    // ---- Applying an answer ---------------------------------------------------------------------

    private async Task<Applied> ApplyAsync(ChooseFolders answer, Snapshot snapshot, bool scanMusic)
    {
        var musicFixed = snapshot.Music.State == FolderRowState.Fixed;
        var analysisFixed = snapshot.Analysis.State == FolderRowState.Fixed;
        var musicPath = musicFixed ? snapshot.Music.Path : answer.Music ?? snapshot.Music.Path;
        var analysisPath = analysisFixed ? snapshot.Analysis.Path : answer.Analysis ?? snapshot.Analysis.Path;

        var checkedFolders = await Task.Run(() => _check.Check(musicPath, analysisPath));
        var musicProblem = musicFixed ? null
            : musicPath is null ? "Choose a music folder." : checkedFolders.MusicProblem;
        var analysisProblem = analysisFixed ? null
            : analysisPath is null ? "Choose an analysis folder." : checkedFolders.AnalysisProblem;
        if (musicProblem is not null || analysisProblem is not null)
            return Retry(snapshot, musicPath, analysisPath, musicProblem, analysisProblem);

        var newRoot = analysisPath is null ? null : _check.Normalize(analysisPath);
        if (analysisFixed)
        {
            _folder.Use(newRoot!);
            await PublishChangedAsync(newRoot!, isOverride: true);
        }
        else
        {
            var problem = await ApplyAnalysisAsync(newRoot!, snapshot);
            if (problem is not null) return Retry(snapshot, musicPath, analysisPath, null, problem);
        }

        if (!musicFixed)
            await _musicDirPreference.SetAsync(musicPath!);
        if (scanMusic)
            await _library.ScanAsync(musicPath!, _database.Stores);
        return new Applied(null, musicPath);
    }

    private Applied Retry(Snapshot snapshot, string? musicPath, string? analysisPath, string? musicProblem, string? analysisProblem) =>
        new(snapshot with
        {
            Music = snapshot.Music with { Path = musicPath, Problem = musicProblem },
            Analysis = snapshot.Analysis with { Path = analysisPath, Problem = analysisProblem },
        }, null);

    // Returns why the analysis folder could not be put in use, or null once it is (and Changed was published).
    private async Task<string?> ApplyAnalysisAsync(string newRoot, Snapshot snapshot)
    {
        var oldRoot = _folder.Root ?? snapshot.OldAnalysisRoot;
        var keeping = oldRoot is not null && _check.Normalize(oldRoot) == newRoot;
        var moving = !keeping && oldRoot is not null && HasStems(oldRoot);

        try
        {
            if (moving)
            {
                // Hold the gate for the whole move: a running demucs finishes first and none start until it ends.
                using (await _gate.EnterAsync())
                {
                    var outcome = await _mover.MoveAsync(
                        oldRoot!, newRoot,
                        (done, total) => _appThread.Post(() => _publisher.Publish(new AnalysisFolderMoveProgress(done, total))),
                        () => CommitAsync(newRoot));
                    if (!outcome.Succeeded) return outcome.Problem;
                }
            }
            else
            {
                // The folder is created only here, after validation, never at startup.
                Directory.CreateDirectory(newRoot);
                await CommitAsync(newRoot);
            }
        }
        catch (Exception ex)
        {
            _foldersLog.Error($"could not use {newRoot}: {ex.Message}");
            return $"Could not use that folder: {ex.Message}";
        }

        try { await _library.HydrateStemStateAsync(); }
        catch (Exception ex) { _foldersLog.Error($"stem state refresh failed: {ex.Message}"); }
        return null;
    }

    private bool HasStems(string root)
    {
        var stems = Path.Combine(root, "stems");
        return Directory.Exists(stems) && Directory.EnumerateDirectories(stems).Any();
    }

    // The analysis folder is now the one in use: remember it, then tell the interfaces.
    private async Task CommitAsync(string newRoot)
    {
        _folder.Use(newRoot);
        await _analysisDirPreference.SetAsync(newRoot);
        await PublishChangedAsync(newRoot, isOverride: false);
    }

    private Task PublishChangedAsync(string path, bool isOverride) =>
        _appThread.InvokeAsync(() => _publisher.Publish(new AnalysisFolderChanged(path, isOverride)));

    private string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
