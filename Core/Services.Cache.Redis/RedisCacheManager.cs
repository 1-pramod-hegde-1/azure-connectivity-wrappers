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
using Services.Cache.Contracts;
using Services.Core.Common;
using Services.Core.Contracts;
using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Services.Cache.Redis
{
    [Export(typeof(ICompositionPart))]
    [StoreType(CacheStore.Redis)]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public sealed partial class RedisCacheManager : ICacheManager
    {
        IRedisCacheProviderConfiguration _configuration;
        IConnectionMultiplexer _client;
        ICacheItemBuilder _valueBuilder;
        RedisDbIndexAnalyzer _indexanalyzer;
        DefaultResponseGenerator _responseGenerator;

        int _dbIndex = 0;
        IDatabase Db
        {
            get
            {
                return _client.GetDatabase(db: _dbIndex);
            }
        }

        bool HasKey (string key)
        {
            if (_configuration.EnableAutoDistribution)
            {
                return new List<int>(Enumerable.Range(0, _configuration.MaxDistributionIndex)).Any(i =>
                {
                    _dbIndex = i;
                    return Db.KeyExists(key);
                });
            }
            else
            {
                return Db.KeyExists(key);
            }
        }

        object ICacheManager.this[string key]
        {
            get
            {
                return Db.StringGet(key);
            }
        }

        int ICacheManager.Count => throw new NotSupportedException();

        string ICompositionPart.Id => "RedisCache";

        bool ICacheManager.Contains (string key)
        {
            if (_client == null)
            {
                return false;
            }

            return HasKey(key);
        }

        async Task<bool> ICacheManager.ContainsAsync (string key)
        {
            return await Task.Run(() =>
            {
                if (_client == null)
                {
                    return false;
                }

                return HasKey(key);
            });
        }

        object ICacheManager.Get (string key)
        {
            if (_client == null || !HasKey(key))
            {
                return null;
            }

            var x = Db.StringGet(key);

            if (x.HasValue && !x.IsNullOrEmpty)
            {
                 return _responseGenerator.Generate(x);
            }

            return null;
        }

        async Task<object> ICacheManager.GetAsync (string key)
        {
            if (_client == null || !HasKey(key))
            {
                return null;
            }

            var x = await Db.StringGetAsync(key);

            if (x.HasValue && !x.IsNullOrEmpty)
            {
                return _responseGenerator.Generate(x);
            }

            return null;
        }

        T ICacheManager.Get<T> (string key)
        {
            if (_client == null || !HasKey(key))
            {
                return default(T);
            }

            var x = Db.StringGet(key);

            if (x.HasValue && !x.IsNullOrEmpty)
            {
                return _responseGenerator.Generate<T>(x);
            }

            return default(T);
        }

        async Task<T> ICacheManager.GetAsync<T> (string key)
        {
            if (_client == null || !HasKey(key))
            {
                return default(T);
            }

            var x = await Db.StringGetAsync(key);

            if (x.HasValue && !x.IsNullOrEmpty)
            {
                return _responseGenerator.Generate<T>(x);
            }

            return default(T);
        }

        void ICacheManager.Initialize<T> (T configuration)
        {
            InitializeSettings(configuration);
            BuildClient();
        }

        private void InitializeSettings<T> (T configuration)
        {
            _configuration = (IRedisCacheProviderConfiguration)configuration;
            Ensure.NotNull("Redis configuration", _configuration);

            if (_configuration.CacheBuilder == null)
            {
                _valueBuilder = new DefaultValueBuilder();
            }
            else
            {
                _valueBuilder = _configuration.CacheBuilder;
            }

            _dbIndex = (int)_configuration.DatabaseId;
            _indexanalyzer = new RedisDbIndexAnalyzer(_configuration);
            _responseGenerator = new DefaultResponseGenerator();
        }

        private void BuildClient ()
        {
            _client = new RedisClientBuilder(_configuration).Build();
        }

        void ICacheManager.Insert (string key, object value)
        {
            if (_client == null)
            {
                return;
            }

            SetDb();
            SetCache(key, value);
        }

        async Task ICacheManager.InsertAsync (string key, object value)
        {
            if (_client == null)
            {
                return;
            }

            SetDb();
            await SetCacheAsync(key, value);
        }

        async Task ICacheManager.InsertAsync (string key, object value, ICacheItemPolicy policy)
        {
            if (_client == null)
            {
                return;
            }

            SetDb();
            await SetCacheWithExpiryAsync(key, policy, value);
        }

        void ICacheManager.Insert (string key, object value, ICacheItemPolicy policy)
        {
            if (_client == null)
            {
                return;
            }

            SetDb();
            SetCacheWithExpiry(key, policy, value);
        }

        void ICacheManager.Remove (string key)
        {
            if (_client == null || !HasKey(key))
            {
                return;
            }

            Db.KeyDelete(key);
        }

        private void SetDb ()
        {
            if (_configuration.EnableAutoDistribution)
            {
                _dbIndex = _indexanalyzer.GetNextIndex();
            }
        }

        private void SetCache (string key, object b)
        {
            Db.StringSet(key, _valueBuilder.BuildCacheItem(b));
        }

        async Task SetCacheAsync (string key, object b)
        {
            await Db.StringSetAsync(key, _valueBuilder.BuildCacheItem(b));
        }

        private void SetCacheWithExpiry (string key, ICacheItemPolicy policy, object b)
        {
            Db.StringSet(key, _valueBuilder.BuildCacheItem(b), expiry: new TimeSpan(policy.AbsoluteExpiration.Hour, policy.AbsoluteExpiration.Minute, policy.AbsoluteExpiration.Second));
        }

        async Task SetCacheWithExpiryAsync (string key, ICacheItemPolicy policy, object b)
        {
            await Db.StringSetAsync(key, _valueBuilder.BuildCacheItem(b), expiry: new TimeSpan(policy.AbsoluteExpiration.Hour, policy.AbsoluteExpiration.Minute, policy.AbsoluteExpiration.Second));
        }        
    }
}