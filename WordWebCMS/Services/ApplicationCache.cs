using Microsoft.Extensions.Caching.Memory;

namespace WordWebCMS.Services
{
    /// <summary>
    /// Application-level cache service to replace HttpApplicationState
    /// </summary>
    public class ApplicationCache
    {
        private readonly IMemoryCache _cache;

        public ApplicationCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        public object? this[string key]
        {
            get => _cache.TryGetValue(key, out var value) ? value : null;
            set
            {
                if (value == null)
                {
                    _cache.Remove(key);
                }
                else
                {
                    _cache.Set(key, value, new MemoryCacheEntryOptions
                    {
                        SlidingExpiration = TimeSpan.FromMinutes(20),
                        Priority = CacheItemPriority.Normal
                    });
                }
            }
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
        }

        public T? Get<T>(string key) where T : class
        {
            return _cache.TryGetValue(key, out T? value) ? value : null;
        }

        public void Set<T>(string key, T value, TimeSpan? slidingExpiration = null) where T : class
        {
            var options = new MemoryCacheEntryOptions
            {
                SlidingExpiration = slidingExpiration ?? TimeSpan.FromMinutes(20),
                Priority = CacheItemPriority.Normal
            };
            _cache.Set(key, value, options);
        }
    }
}
