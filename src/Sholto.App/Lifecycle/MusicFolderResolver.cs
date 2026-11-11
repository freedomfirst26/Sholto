using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Lifecycle;

/// <summary>See <see cref="IMusicFolderResolver"/>: finds the folder to scan (override, saved setting, or the
/// question to the user), saves a picked folder, scans it, and handles the menu's change request.</summary>
public sealed class MusicFolderResolver(
    ILibraryDatabase database,
    ILibrarySession library,
    ISettingPreference musicDirPreference,
    IAppThread appThread,
    IEventPublisher publisher,
    string? musicDirOverride) : IMusicFolderResolver
{
    private readonly ILibraryDatabase _database = database;
    private readonly ILibrarySession _library = library;
    private readonly ISettingPreference _musicDirPreference = musicDirPreference;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;
    // The SHOLTO_MUSIC_DIR environment override, read once by the composition root.
    private readonly string? _musicDirOverride = musicDirOverride;

    // The question currently put to the user, if any: completed by the matching Choose command.
    private TaskCompletionSource<string?>? _musicFolderAnswer;

    public async Task<bool> ResolveAndScanAsync()
    {
        var saved = await _musicDirPreference.GetAsync();
        var musicDir = _musicDirOverride ?? saved;

        // Ask again when there's no saved path (first run) OR the saved path no longer resolves — drive
        // unmounted, or remounted under a different name (/media/s/Data vs /media/s/Data1). Silently
        // skipping left a blank library with no way back short of a restart.
        bool unreachable = !string.IsNullOrEmpty(musicDir) && !Directory.Exists(musicDir);
        if (string.IsNullOrEmpty(musicDir) || unreachable)
        {
            if (unreachable)
            {
                Console.WriteLine($"[Library] saved music dir not reachable: {musicDir} — re-prompting");
                await _appThread.InvokeAsync(() => _library.SetUnreachablePath(musicDir));
            }
            var picked = await RequestMusicFolderAsync(
                unreachable ? MusicFolderReason.DriveNotFound : MusicFolderReason.FirstRun,
                unreachable ? musicDir : null);
            if (string.IsNullOrEmpty(picked)) return false;  // cancelled — keep the saved path for next launch
            musicDir = picked;
            await _musicDirPreference.SetAsync(musicDir);
        }

        await _library.ScanAsync(musicDir!, _database.Stores);
        return true;
    }

    /// <summary>Menu entry point: ask for a new music folder, persist it, then re-scan. Nothing happens if
    /// the user cancels. Re-scans even when the same folder is picked again — important for the banner
    /// re-pick flow, where confirming the same path (after the drive re-mounts) should reload the library.</summary>
    public void Handle(in ChangeMusicFolder command) => _ = ChangeMusicFolderAsync();

    private async Task ChangeMusicFolderAsync()
    {
        if (_musicFolderAnswer is not null) return;  // already asking
        var picked = await RequestMusicFolderAsync(MusicFolderReason.Change, null);
        if (string.IsNullOrEmpty(picked)) return;

        await _musicDirPreference.SetAsync(picked);

        await _library.ScanAsync(picked, _database.Stores);
    }

    public void Handle(in ChooseMusicFolder command)
    {
        var answer = _musicFolderAnswer;
        if (answer is null) return;
        _musicFolderAnswer = null;
        answer.TrySetResult(command.Path);
    }

    private async Task<string?> RequestMusicFolderAsync(MusicFolderReason reason, string? missingPath)
    {
        var answer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _appThread.InvokeAsync(() =>
        {
            _musicFolderAnswer = answer;
            _publisher.Publish(new MusicFolderNeeded(reason, missingPath));
        });
        return await answer.Task.ConfigureAwait(false);
    }
}
