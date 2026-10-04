namespace Sholto.App.Lifecycle;

/// <summary>One persisted setting, addressed by a fixed key. Waits for the library
/// database to finish opening; when it is unavailable, reads return null and writes
/// do nothing.</summary>
public interface ISettingPreference
{
    Task<string?> GetAsync();

    Task SetAsync(string value);
}
