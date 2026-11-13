using Sholto.Data;

namespace Sholto.App.Tests.Recording;

/// <summary>Keeps every event it is published, in order.</summary>
internal sealed class CapturingPublisher : IEventPublisher
{
    public List<IEvent> Events { get; } = [];

    public void Publish<T>(in T e) where T : struct, IEvent => Events.Add(e);

    public List<T> Of<T>() where T : struct, IEvent => Events.OfType<T>().ToList();
}
