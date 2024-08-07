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
using Services.Core.Contracts;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Warden;
using Warden.Watchers;
using Warden.Watchers.Redis;

namespace Services.Core.Monitoring
{
    [Export(typeof(ICompositionPart))]
    [ComponentType(DependentComponentTypes.RedisCache)]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public sealed class RedisMonitor : AbstractMonitorBase<IRedisCacheProviderConfiguration, WardenWatchDog>
    {
        public override string Id => "RedisCacheMonitor";

        ConfigurationOptions RedisProperties => ConfigurationOptions.Parse(_setting?.ConnectionString);

        public override async Task<IEnumerable<DependentComponentHealth>> CheckHealth ()
        {
            await _watchDog.ExecuteAsync();
            return _health;
        }

        public override IDependentComponent CreatePart ()
        {
            return new RedisMonitor();
        }

        IWatcher Watcher
        {
            get
            {
                IWatcher w = null;                

                if (_needsCaching && _cacheManager!=null)
                {
                    var k = RedisProperties.ToString(false);
                    if (_cacheManager.Contains(k))
                    {
                        w = (IWatcher)_cacheManager.Get(k);
                    }
                    else
                    {
                        w = RedisWatcher.Create(connectionString: _setting.ConnectionString, database: (int)_setting.DatabaseId);
                        _cacheManager.Insert(k, w, new DefaultCacheItemPolicy());
                    }
                }
                else
                {
                    w = RedisWatcher.Create(connectionString: _setting.ConnectionString, database: (int)_setting.DatabaseId);
                }

                return w;
            }
        }

        public override Task Initialize ()
        {
            if (_watchDog == null)
            {
                _watchDog = new WardenWatchDog();
            }

            _watchDog.AddWatcher(Watcher);
            _watchDog.OnSuccess(c => OnCompletion(c));
            _watchDog.OnFailure(c => OnCompletion(c));
            _watchDog.OnError(e => HandleError(e));

            return Task.CompletedTask;
        }

        private void HandleError (Exception e)
        {
            _health.Add(new DependentComponentHealth
            {
                ComponentType = DependentComponentTypes.RedisCache,
                IsValid = false,
                Connection = RedisProperties.ToString(false),
                Identifier = RedisProperties.ClientName,
                ErrorDetails = e.ToString()
            });
        }

        private void OnCompletion (IWardenCheckResult c)
        {
            _health.Add(new DependentComponentHealth
            {
                ComponentType = DependentComponentTypes.RedisCache,
                IsValid = c.IsValid,
                Connection = RedisProperties.ToString(false),
                Identifier = RedisProperties.ClientName,
                ErrorDetails = c.Exception?.ToString(),
                Description = c.WatcherCheckResult?.Description,
                ExecutionTime = c.ExecutionTime,
                StartedAt = c.StartedAt,
                CompletedAt = c.CompletedAt
            });
        }
    }
}
