using System.Collections.Concurrent;

namespace Sholto.Core;

/// <summary>
/// Generic in-process cache-aside helper, backed by <see cref="ConcurrentDictionary{TKey,TValue}"/>.
///
/// A cache is not itself a port — it never appears behind an interface. It is a
/// concrete wrapper around a concrete collaborator: the caller says "do this thing",
/// and if it's been done before for this key, the cached result comes back instead;
/// otherwise the wrapper does the thing and remembers the result. See
/// <c>CachingStemAnalysisStep</c> (wraps <c>IStemAnalysisStep</c>) and
/// <c>CachingAnalysisProvider</c> (wraps <c>IAnalysisProvider</c>) — both
/// are decorators that implement the same port as the thing they wrap, and both hold
/// one of these to do the remembering. Copy that shape for the next cache: no new
/// interface, just another decorator holding a <see cref="MemoryCache{TKey,TValue}"/>.
///
/// This is the rule the type embodies: a cache never appears on an interface. It is
/// always a concrete wrapper implementing the SAME port as the thing it wraps, so the
/// caller just calls the port and never knows caching exists. That is why this type is
/// generic and lives in a shared leaf rather than next to any one port.
///
/// Deliberately minimal: no eviction, expiry, size limit or statistics. Nobody has
/// asked for those.
/// </summary>
public sealed class MemoryCache<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _byKey = new();

    /// <summary>Return the cached value for <paramref name="key"/>, or null if absent.</summary>
    public TValue? TryGet(TKey key) => _byKey.TryGetValue(key, out var value) ? value : default;

    /// <summary>
    /// Cache-aside: return the cached value for <paramref name="key"/> if present;
    /// otherwise run <paramref name="compute"/>, store its result, and return it.
    /// </summary>
    public async Task<TValue> GetOrComputeAsync(TKey key, Func<Task<TValue>> compute)
    {
        if (_byKey.TryGetValue(key, out var cached))
            return cached;

        var computed = await compute();
        _byKey[key] = computed;
        return computed;
    }

    /// <summary>Unconditionally store <paramref name="value"/> for <paramref name="key"/>, overwriting any existing entry.</summary>
    public void Set(TKey key, TValue value) => _byKey[key] = value;
}
