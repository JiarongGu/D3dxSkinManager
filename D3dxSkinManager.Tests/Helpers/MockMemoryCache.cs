using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Moq;

namespace D3dxSkinManager.Tests.Helpers;

/// <summary>
/// Simple mock implementation of IMemoryCache for testing business logic
/// Focuses on testing when/what to cache, not the caching mechanism itself
/// </summary>
public class MockMemoryCache
{
    private readonly Mock<IMemoryCache> _mock;
    private readonly Dictionary<object, object?> _cache = new();
    private readonly HashSet<object> _removedKeys = new();

    public Mock<IMemoryCache> Mock => _mock;
    public IMemoryCache Object => _mock.Object;

    /// <summary>
    /// Track what keys have been cached
    /// </summary>
    public IReadOnlyDictionary<object, object?> CachedItems => _cache;

    /// <summary>
    /// Track what keys have been removed
    /// </summary>
    public IReadOnlySet<object> RemovedKeys => _removedKeys;

    /// <summary>
    /// Count of cache hits (successful TryGetValue calls)
    /// </summary>
    public int CacheHits { get; private set; }

    /// <summary>
    /// Count of cache misses (failed TryGetValue calls)
    /// </summary>
    public int CacheMisses { get; private set; }

    public MockMemoryCache()
    {
        _mock = new Mock<IMemoryCache>();
        SetupMocks();
    }

    private void SetupMocks()
    {
        // Setup TryGetValue - the main cache retrieval method
        _mock.Setup(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object?>.IsAny))
            .Returns(new TryGetValueDelegate((object key, out object? value) =>
            {
                if (_cache.TryGetValue(key, out value))
                {
                    CacheHits++;
                    return true;
                }
                CacheMisses++;
                value = null;
                return false;
            }));

        // Setup CreateEntry for cache population
        _mock.Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns<object>(key =>
            {
                var mockEntry = new MockCacheEntry(key, value =>
                {
                    _cache[key] = value;
                });
                return mockEntry;
            });

        // Setup Remove
        _mock.Setup(x => x.Remove(It.IsAny<object>()))
            .Callback<object>(key =>
            {
                _cache.Remove(key);
                _removedKeys.Add(key);
            });
    }

    /// <summary>
    /// Helper to directly set a cache value (simulates cache hit)
    /// </summary>
    public void SetValue(object key, object? value)
    {
        _cache[key] = value;
    }

    /// <summary>
    /// Helper to check if a key is cached
    /// </summary>
    public bool HasKey(object key)
    {
        return _cache.ContainsKey(key);
    }

    /// <summary>
    /// Helper to get a cached value
    /// </summary>
    public T? GetValue<T>(object key)
    {
        return _cache.TryGetValue(key, out var value) ? (T?)value : default;
    }

    /// <summary>
    /// Clear all cached items and tracking
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _removedKeys.Clear();
        CacheHits = 0;
        CacheMisses = 0;
    }

    /// <summary>
    /// Verify that a key was cached
    /// </summary>
    public void VerifyCached(object key)
    {
        if (!_cache.ContainsKey(key))
        {
            throw new InvalidOperationException($"Key '{key}' was not cached");
        }
    }

    /// <summary>
    /// Verify that a key was removed
    /// </summary>
    public void VerifyRemoved(object key)
    {
        if (!_removedKeys.Contains(key))
        {
            throw new InvalidOperationException($"Key '{key}' was not removed");
        }
    }

    /// <summary>
    /// Verify cache statistics
    /// </summary>
    public void VerifyStats(int expectedHits, int expectedMisses)
    {
        if (CacheHits != expectedHits)
        {
            throw new InvalidOperationException($"Expected {expectedHits} cache hits but got {CacheHits}");
        }
        if (CacheMisses != expectedMisses)
        {
            throw new InvalidOperationException($"Expected {expectedMisses} cache misses but got {CacheMisses}");
        }
    }

    // Delegate for TryGetValue
    private delegate bool TryGetValueDelegate(object key, out object? value);
}

/// <summary>
/// Simple mock implementation of ICacheEntry for testing
/// </summary>
internal class MockCacheEntry : ICacheEntry
{
    private readonly object _key;
    private readonly Action<object?> _setValue;
    private object? _value;

    public MockCacheEntry(object key, Action<object?> setValue)
    {
        _key = key;
        _setValue = setValue;
    }

    public object Key => _key;

    public object? Value
    {
        get => _value;
        set
        {
            _value = value;
            _setValue(value);
        }
    }

    public DateTimeOffset? AbsoluteExpiration { get; set; }
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
    public IList<IChangeToken> ExpirationTokens { get; } = new List<IChangeToken>();
    public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; } = new List<PostEvictionCallbackRegistration>();
    public CacheItemPriority Priority { get; set; } = CacheItemPriority.Normal;
    public long? Size { get; set; }

    public void Dispose()
    {
        // No-op for testing
    }
}

/// <summary>
/// Extension methods for easier testing with GetOrCreateAsync pattern
/// </summary>
public static class MockMemoryCacheExtensions
{
    /// <summary>
    /// Setup GetOrCreateAsync to use mock cache storage
    /// </summary>
    public static void SetupGetOrCreateAsync<T>(this MockMemoryCache mockCache)
    {
        // GetOrCreateAsync is an extension method, so we can't mock it directly
        // Tests should use TryGetValue and CreateEntry which are already mocked
    }
}