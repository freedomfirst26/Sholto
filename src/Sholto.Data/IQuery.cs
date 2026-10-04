namespace Sholto.Data;

/// <summary>Marker: "I want to know". A query is a <c>readonly record struct</c> answered by exactly
/// one App handler with a <typeparamref name="TResult"/>. A query never changes state.</summary>
public interface IQuery<TResult>;
