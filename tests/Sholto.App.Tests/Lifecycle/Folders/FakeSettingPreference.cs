using Sholto.App.Lifecycle;

namespace Sholto.App.Tests;

/// <summary>One setting held in memory.</summary>
internal sealed class FakeSettingPreference(string? value = null) : ISettingPreference
{
    public string? Value { get; private set; } = value;

    public Task<string?> GetAsync() => Task.FromResult(Value);

    public Task SetAsync(string value)
    {
        Value = value;
        return Task.CompletedTask;
    }
}
