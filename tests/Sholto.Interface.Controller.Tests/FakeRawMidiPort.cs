using Sholto.Interface.Controller;

namespace Sholto.Interface.Controller.Tests;

/// <summary>An <see cref="IRawMidiPort"/> that records every byte array sent and lets a test raise
/// incoming messages and a device loss. <see cref="Disposed"/> completes when the port is disposed,
/// so a test can await an off-thread dispose without sleeping.</summary>
public sealed class FakeRawMidiPort : IRawMidiPort
{
    private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<byte[]> Sent { get; } = [];
    public Task Disposed => _disposed.Task;
    public bool IsDisposed => _disposed.Task.IsCompleted;

    public event Action<byte, byte, byte>? MessageReceived;
    public event Action? Disconnected;

    public void Send(byte[] bytes) => Sent.Add(bytes);
    public void Receive(byte status, byte data1, byte data2) => MessageReceived?.Invoke(status, data1, data2);
    public void Lose() => Disconnected?.Invoke();
    public void Dispose() => _disposed.TrySetResult();
}
