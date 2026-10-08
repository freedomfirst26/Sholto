namespace Sholto.App.Audio;

/// <summary>What <see cref="AudioEngine.Start(string)"/> did with master: the device it plays on and how it got
/// there. Returned, never stored, so it cannot go stale behind the caller's back.</summary>
/// <param name="DeviceName">The device master plays on. Null means nothing plays.</param>
/// <param name="Reason">How master got there and why, in words for the log.</param>
public readonly record struct MasterRoute(string? DeviceName, string Reason);
