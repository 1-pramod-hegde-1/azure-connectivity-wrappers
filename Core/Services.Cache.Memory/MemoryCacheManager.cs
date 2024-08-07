// Copyright (c) [2022] Pramod Hegde
//
// This library is designed to simplify connectivity to Azure resources.
// You may copy and distribute this code as long as this copyright notice is retained.
// For business purposes, the copyright notice must remain intact.
//
// This license is based on the MIT License, with additional conditions as specified below.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
using Services.Core.Contracts;
using System;
using System.ComponentModel.Composition;
using System.Runtime.Caching;
using System.Threading.Tasks;

namespace Services.Cache.Memory
{
    [Export(typeof(ICompositionPart))]
    [StoreType(CacheStore.MemoryCache)]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public sealed class MemoryCacheManager : ICacheManager
    {
        MemoryCache _cache = MemoryCache.Default;

        object _mutex = new object();

        object ICacheManager.this[string key]
        {
            get
            {
                if (_cache == null || !_cache.Contains(key))
                {
                    return null;
                }
                return _cache.Get(key);
            }
        }

        int ICacheManager.Count
        {
            get
            {
                if (_cache == null)
                {
                    return -1;
                }

                return (int)_cache.GetCount();
            }
        }

        string ICompositionPart.Id => "MemCache";

        bool ICacheManager.Contains (string key)
        {
            return _cache.Contains(key);
        }

        Task<bool> ICacheManager.ContainsAsync (string key)
        {
            throw new NotSupportedException();
        }

        object ICacheManager.Get (string key)
        {
            if (_cache == null || !_cache.Contains(key))
            {
                return null;
            }
            return _cache.Get(key);
        }

        T ICacheManager.Get<T> (string key)
        {
            throw new NotSupportedException();
        }

        async Task<object> ICacheManager.GetAsync (string key)
        {
            if (_cache == null || !_cache.Contains(key))
            {
                return null;
            }

            return await Task.Run(() => _cache.Get(key));
        }

        Task<T> ICacheManager.GetAsync<T> (string key)
        {
            throw new NotSupportedException();
        }

        void ICacheManager.Initialize<T> (T configuration)
        {
            throw new NotSupportedException();
        }

        void ICacheManager.Insert (string key, object value)
        {
            if (_cache != null && !_cache.Contains(key))
            {
                lock (_mutex)
                {
                    _cache.Add(key, value, DateTime.UtcNow.Add(new TimeSpan(24, 0, 0)));
                };
            }
        }

        void ICacheManager.Insert (string key, object value, ICacheItemPolicy policy)
        {
            if (_cache != null && !_cache.Contains(key))
            {
                lock (_mutex)
                {
                    _cache.Add(key, value, new CacheItemPolicyBuilder(policy).Build());
                }
            }
        }

        async Task ICacheManager.InsertAsync (string key, object value)
        {
            if (_cache != null && !_cache.Contains(key))
            {
                await Task.Run(() => _cache.Add(key, value, DateTime.UtcNow.Add(new TimeSpan(24, 0, 0))));
            }
        }

        async Task ICacheManager.InsertAsync (string key, object value, ICacheItemPolicy policy)
        {
            if (_cache != null && !_cache.Contains(key))
            {
                await Task.Run(() => _cache.Add(key, value, new CacheItemPolicyBuilder(policy).Build()));
            }
        }

        void ICacheManager.Remove (string key)
        {
            if (_cache != null && _cache.Contains(key))
            {
                lock (_mutex)
                {
                    _cache.Remove(key);
                }
            }
        }
    }
}
