using Sholto.Data;

namespace Sholto.App.Lifecycle;

/// <summary>Finds the music folder to scan, asks the user when there is none, and handles the menu's change request.</summary>
public interface IMusicFolderResolver : ICommandHandler<ChangeMusicFolder>, ICommandHandler<ChooseMusicFolder>
{
    /// <summary>Startup: override, then saved setting, then the picker (first run or drive not found); a picked
    /// folder is saved, then the folder is scanned. True once the scan ran; false when the picker was cancelled
    /// (nothing scanned).</summary>
    Task<bool> ResolveAndScanAsync();
}
