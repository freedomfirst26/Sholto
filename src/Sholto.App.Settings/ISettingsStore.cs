namespace Sholto.App.Settings;

/// <summary>Persistent key/value user settings. Keys come from <see cref="SettingsKeys"/>.
/// There is no delete and no synchronous read: every current caller reads once at
/// startup or on a picker action and writes whole values.</summary>
public interface ISettingsStore
{
    /// <summary>The stored value, or null when the key has never been set.</summary>
    Task<string?> GetAsync(string key);

    /// <summary>Insert or overwrite the value for <paramref name="key"/>.</summary>
    Task SetAsync(string key, string value);
}
