using System;
using System.Collections.Generic;
using System.Threading;

namespace D3dxSkinManager.Modules.Core.Utilities;

/// <summary>
/// Thread-safe LRU (Least Recently Used) cache implementation with size limit
/// </summary>
/// <typeparam name="TKey">The type of keys in the cache</typeparam>
/// <typeparam name="TValue">The type of values in the cache</typeparam>
public class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly ReaderWriterLockSlim _lock;

    private class CacheItem
    {
        public TKey Key { get; set; } = default!;
        public TValue Value { get; set; } = default!;
    }

    /// <summary>
    /// Initializes a new instance of the LruCache class
    /// </summary>
    /// <param name="capacity">Maximum number of items to store in the cache</param>
    public LruCache(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));

        _capacity = capacity;
        _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
        _lruList = new LinkedList<CacheItem>();
        _lock = new ReaderWriterLockSlim();
    }

    /// <summary>
    /// Gets the number of items currently in the cache
    /// </summary>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _cache.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets or adds a value to the cache
    /// </summary>
    /// <param name="key">The key to lookup or add</param>
    /// <param name="valueFactory">Factory function to create the value if not present</param>
    /// <returns>The cached or newly created value</returns>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        // Try to get from cache with read lock
        _lock.EnterReadLock();
        try
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // Move to front (most recently used)
                _lock.ExitReadLock();
                _lock.EnterWriteLock();
                try
                {
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    return node.Value.Value;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }
        finally
        {
            if (_lock.IsReadLockHeld)
                _lock.ExitReadLock();
        }

        // Not in cache, compute and add with write lock
        _lock.EnterWriteLock();
        try
        {
            // Double-check in case another thread added it
            if (_cache.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return node.Value.Value;
            }

            // Compute value
            var value = valueFactory(key);

            // Add to cache
            var cacheItem = new CacheItem { Key = key, Value = value };
            var newNode = _lruList.AddFirst(cacheItem);
            _cache[key] = newNode;

            // Remove oldest if over capacity
            if (_cache.Count > _capacity)
            {
                var lastNode = _lruList.Last;
                if (lastNode != null)
                {
                    _lruList.RemoveLast();
                    _cache.Remove(lastNode.Value.Key);
                }
            }

            return value;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Tries to get a value from the cache without adding
    /// </summary>
    /// <param name="key">The key to lookup</param>
    /// <param name="value">The value if found</param>
    /// <returns>True if the key was found, false otherwise</returns>
    public bool TryGetValue(TKey key, out TValue value)
    {
        _lock.EnterReadLock();
        try
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // Move to front (most recently used)
                _lock.ExitReadLock();
                _lock.EnterWriteLock();
                try
                {
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    value = node.Value.Value;
                    return true;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            value = default!;
            return false;
        }
        finally
        {
            if (_lock.IsReadLockHeld)
                _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Removes a specific key from the cache
    /// </summary>
    /// <param name="key">The key to remove</param>
    /// <returns>True if the key was removed, false if not found</returns>
    public bool Remove(TKey key)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_cache.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _cache.Remove(key);
                return true;
            }
            return false;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clears all items from the cache
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _cache.Clear();
            _lruList.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Disposes the cache and releases the lock
    /// </summary>
    public void Dispose()
    {
        _lock?.Dispose();
    }
}