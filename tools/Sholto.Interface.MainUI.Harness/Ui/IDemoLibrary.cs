using Sholto.App.Library;

namespace Sholto.Interface.MainUI.Harness.Ui;

/// <summary>A real library database the harness fills with made-up crates and tags, so Glance's chips narrow by the
/// same crate membership and tag lookups the app uses. Dev-only; never part of the app. Disposing removes the temp database file.</summary>
public interface IDemoLibrary : IDisposable
{
    /// <summary>The scan stores over the demo database (opened on first use), for a scan to give its tracks ids.</summary>
    Task<LibraryStack> OpenAsync();

    /// <summary>Creates the demo crates, files tracks into them and tags tracks, through the real services, then
    /// attaches those services to <paramref name="library"/> as the app does once its database is up.</summary>
    Task SeedAsync(ILibrarySession library);
}
