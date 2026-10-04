using Sholto.App.Settings;

namespace Sholto.App.Tests;

/// <summary>Settings held in memory.</summary>
internal sealed class FakeSettingsStore : ISettingsStore
{
    private readonly Dictionary<string, string> _values = [];

    public Task<string?> GetAsync(string key) =>
        Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

    public Task SetAsync(string key, string value)
    {
        lock (_values) _values[key] = value;
        return Task.CompletedTask;
    }

    /// <summary>The stored value, or null.</summary>
    public string? Peek(string key)
    {
        lock (_values) return _values.TryGetValue(key, out var value) ? value : null;
    }
}
