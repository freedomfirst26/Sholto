namespace Sholto.App.Audio.Tests.Recording;

/// <summary>A fresh folder under the temp folder, not yet created, deleted with everything in it on dispose.</summary>
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sholto-rec-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
    }
}
