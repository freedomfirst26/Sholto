using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Stems;
using Sholto.App.Lifecycle.Folders;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>A <see cref="LibraryFolders"/> over a real session, real stem gate and analysis folder, in-memory
/// preferences and (unless given another) the real mover and rename. Every event it publishes is recorded; a
/// test queues the answers the "interface" gives to each <see cref="FoldersNeeded"/>. Temp folders are
/// removed on dispose.</summary>
internal sealed class FoldersRig : IDisposable
{
    private readonly List<string> _dirs = [];
    private readonly Queue<ChooseFolders> _answers = new();

    public FoldersRig(
        string? musicOverride = null,
        string? analysisOverride = null,
        string? savedMusic = null,
        string? savedAnalysis = null,
        IStemFolderMover? mover = null,
        string? legacyRoot = null)
    {
        var appThread = new SingleThreadAppThread();
        Library = new LibrarySessionRig(appThread);
        Music = new FakeSettingPreference(savedMusic);
        Analysis = new FakeSettingPreference(savedAnalysis);
        Folder = new AnalysisFolder();
        Gate = new SemaphoreStemGate();
        Legacy = legacyRoot ?? NewDir("sholto-legacy-");
        var options = Options.Create(new AnalysisFolderOptions());
        Folders = new LibraryFolders(
            Music, Analysis, Folder, Gate,
            mover ?? new StemFolderMover(new DirectoryRename(), options),
            new FolderCheck(options), Library.Library, new FakeLibraryDatabase(null), appThread, Library.Bus,
            musicOverride, analysisOverride, Legacy);
        Library.Bus.Subscribe(Needed);
        Library.Bus.Subscribe(Changed);
        Library.Bus.Subscribe(Progress);
        Library.Bus.Subscribe(new ActionEventHandler<FoldersNeeded>(_ =>
        {
            if (_answers.TryDequeue(out var answer)) Folders.Handle(answer);
        }));
    }

    public Origin Origin { get; } = new(InterfaceIds.Bench, "test", "answer");
    public LibrarySessionRig Library { get; }
    public FakeSettingPreference Music { get; }
    public FakeSettingPreference Analysis { get; }
    public AnalysisFolder Folder { get; }
    public SemaphoreStemGate Gate { get; }
    public string Legacy { get; }
    public LibraryFolders Folders { get; }

    public RecordingHandler<FoldersNeeded> Needed { get; } = new();
    public RecordingHandler<AnalysisFolderChanged> Changed { get; } = new();
    public RecordingHandler<AnalysisFolderMoveProgress> Progress { get; } = new();

    public string NewDir(string prefix = "sholto-folders-")
    {
        var dir = Directory.CreateTempSubdirectory(prefix).FullName;
        _dirs.Add(dir);
        return dir;
    }

    /// <summary>A folder that is not there yet, inside a new temp folder (so its parent exists).</summary>
    public string NewMissingDir(string name = "Not Yet") => Path.Combine(NewDir(), name);

    /// <summary>The interface's next reply to a <see cref="FoldersNeeded"/>.</summary>
    public void Answer(string? music, string? analysis) => _answers.Enqueue(new ChooseFolders(music, analysis, Origin));

    public async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }

    public void Dispose()
    {
        foreach (var dir in _dirs)
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
