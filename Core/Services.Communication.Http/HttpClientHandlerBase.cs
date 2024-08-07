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
using Services.Core.Common;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;

namespace Services.Communication.Http
{
    abstract class HttpClientHandlerBase<T, TCache> : IHttpClientHandlerBase
    {
        protected HttpClient _client;
        ClientCacheHandler<TCache> _cache;
        ConcurrentDictionary<Type, ClientCacheHandler<TCache>> _cacheStores = new ConcurrentDictionary<Type, ClientCacheHandler<TCache>>();
        protected IHttpClientConfig _config;
        protected T _authResponse;
        protected object _mutext = new object();

        internal HttpClientHandlerBase(IHttpClientConfig config, T authResponse, TCache cacheManager = default)
        {
            _cache = GetClientCacheHandler(cacheManager);
            _config = config;
            _authResponse = authResponse;
        }

        private ClientCacheHandler<TCache> GetClientCacheHandler(TCache cacheManager)
        {
            if (_cacheStores.ContainsKey(typeof(TCache)))
            {
                return _cacheStores[typeof(TCache)];
            }

            var type = typeof(ICacheHandler);
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(p => type.IsAssignableFrom(p));

            foreach (var m in types)
            {
                if (m.Attributes.HasFlag(TypeAttributes.Abstract))
                {
                    continue;
                }

                if (m.Attributes.HasFlag(TypeAttributes.Sealed))
                {
                    if (((TypeInfo)cacheManager.GetType()).ImplementedInterfaces.Any(t => t == m.GetConstructors().First().GetParameters().First().ParameterType))
                    {
                        using (var semaphore = new Semaphore(0, 1, Guid.NewGuid().ToString(), out bool createNew))
                        {
                            if (createNew)
                            {
                                _cacheStores[typeof(TCache)] = Activator.CreateInstance(m, cacheManager) as ClientCacheHandler<TCache>;                                
                                semaphore.Release();
                            }
                        }
                        
                        return _cacheStores[typeof(TCache)];
                    }
                }
            }

            return default;
        }

        bool NeedsCaching => _cache != null;

        bool CheckClientExistsInCache => _cache?.Contains(_config.BaseUri) ?? false;

        protected void CreateClient()
        {
            if (NeedsCaching)
            {
                if (CheckClientExistsInCache)
                {
                    _client = (HttpClient)_cache?.Get(_config.BaseUri);
                    _client.DefaultRequestHeaders.Clear();
                    Decorate();
                }
                else
                {
                    _client = CreateClientInternal();
                    Decorate();
                    _cache?.Insert(_config.BaseUri, _client);
                }
            }
            else
            {
                _client = CreateClientInternal();
                Decorate();
            }
        }

        private HttpClient CreateClientInternal()
        {
            if (NeedsHttpHandler)
            {
                return new HttpClient(WebHandler);
            }
            return new HttpClient();
        }

        internal virtual void Decorate()
        {
            CustomizeClient();

            if (_config.HeaderProperties == null || !_config.HeaderProperties.Any())
            {
                return;
            }

            foreach (var c in _config.HeaderProperties)
            {
                if (_client.DefaultRequestHeaders.Contains(c.Key))
                {
                    continue;
                }
                _client.DefaultRequestHeaders.Add(c.Key, c.Value);
            }
        }

        private void CustomizeClient()
        {
            if (!CheckClientExistsInCache)
            {
                _client.BaseAddress = new Uri(_config.BaseUri);
                _client.Timeout = _config.ConnectionTimeout;
            }

            if (!_client.DefaultRequestHeaders.Contains("ConnectionClose"))
            {
                _client.DefaultRequestHeaders.ConnectionClose = true;
            }

            if (!_client.DefaultRequestHeaders.Contains("Accept"))
            {
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
        }


        public abstract HttpClient GetClient();
        internal abstract bool NeedsHttpHandler { get; }
        internal abstract HttpClientHandler WebHandler { get; }
    }
}