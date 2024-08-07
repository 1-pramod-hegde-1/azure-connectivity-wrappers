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
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Services.Core.Common;
using Services.Core.Contracts;
using Microsoft.Azure.Documents;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

namespace Services.Data.CosmosDb
{
    [Export(typeof(ICompositionPart))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CosmosDbHandler : ICosmosDbHandler
    {
        CosmosClient _client;
        string _database, _collection;
        ConcurrentDictionary<Type, dynamic> _cacheStores = new ConcurrentDictionary<Type, dynamic>();

        string ICompositionPart.Id => "CosmosDbHandler";

        public void Dispose()
        {
        }

        public CosmosDbHandler()
        {
            Debugger.Log(1, "CosmosDbHandler", $"CosmosDbHandler ctor called");
        }

        public void Initialize<TCache>(ICosmosDbAccessAdapterInstanceConfiguration<TCache> setting)
        {
            ValidateConectionString(setting.Connection);
            LoadDocumentClient(setting);
            Debugger.Log(1, "CosmosDbHandler", $"CosmosDbHandler initialized");
        }

        private void LoadDocumentClient<TCache>(ICosmosDbAccessAdapterInstanceConfiguration<TCache> setting)
        {
            CosmosDbConnection connectionSettings = setting.Connection;
            _database = connectionSettings.Database;
            _collection = setting.Collection;

            if (setting.CacheManager == null)
            {
                Debugger.Log(1, "CosmosDbHandler", $"CacheManager is not found. Client will not be cached");
                _client = CreateClient(setting);
            }
            else
            {
                Debugger.Log(1, "CosmosDbHandler", $"CacheManager is found");

                ClientCacheHandler<TCache> _cache = GetClientCacheHandler(setting.CacheManager);

                if (_cache == null)
                {
                    Debugger.Log(1, "CosmosDbHandler", $"Cache handler is empty");
                    _client = CreateClient(setting);
                }

                else
                {
                    Debugger.Log(1, "CosmosDbHandler", $"Cache handler is not empty");

                    string key = GetCacheKey(setting, connectionSettings);
                    if (_cache.Contains(key))
                    {
                        Debugger.Log(1, "CosmosDbHandler", $"CosmosClient is retreived from cache. Key={key}");
                        _client = (CosmosClient)_cache.Get(key);
                    }
                    else
                    {
                        Debugger.Log(1, "CosmosDbHandler", $"CosmosClient is being cached. Key={key}");
                        _client = CreateClient(setting);
                        _cache.Insert(key, _client);
                    }
                }
            }
        }

        private string GetCacheKey<TCache>(ICosmosDbAccessAdapterInstanceConfiguration<TCache> setting, CosmosDbConnection connectionSettings)
        {
            bool readOnlyRequest = (setting.UseReadOnlyConnection && setting.PreferredReadLocations != null && setting.PreferredReadLocations.Count > 0);
            string requestType = readOnlyRequest ? "readOnly" : "readWrite";
            return $"{connectionSettings.AccountEndPoint}:{_database}:{_collection}:{requestType}";
        }

        private ClientCacheHandler<TCache> GetClientCacheHandler<TCache>(TCache cacheManager)
        {
            if (_cacheStores.ContainsKey(typeof(TCache)))
            {
                Debugger.Log(1, "CosmosDbHandler", $"Found matching entry for {nameof(TCache)}. Returning the value from _cacheStores");
                return _cacheStores[typeof(TCache)] as ClientCacheHandler<TCache>;
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
                                Debugger.Log(1, "CosmosDbHandler", $"Updating _cacheStores");
                                semaphore.Release();
                            }
                        }

                        return _cacheStores[typeof(TCache)] as ClientCacheHandler<TCache>;
                    }
                }
            }

            return default;
        }

        private CosmosClient CreateClient<TCache>(ICosmosDbAccessAdapterInstanceConfiguration<TCache> setting)
        {
            CosmosDbConnection connectionSettings = setting.Connection as CosmosDbConnection;
            CosmosClient client;

            if (setting.Connection.UseManagedIdentity)
            {
                var options = new DefaultAzureCredentialOptions
                {
                    ExcludeEnvironmentCredential = false,
                    ExcludeManagedIdentityCredential = false,
                    ExcludeSharedTokenCacheCredential = false,
                    ExcludeVisualStudioCredential = false,
                    ExcludeVisualStudioCodeCredential = false,
                    ExcludeAzureCliCredential = false,
                    ExcludeInteractiveBrowserCredential = true,
                    SharedTokenCacheTenantId = connectionSettings.TenantId,
                    VisualStudioCodeTenantId = connectionSettings.TenantId,
                    VisualStudioTenantId = connectionSettings.TenantId
                };

                client = new CosmosClient(connectionSettings.AccountEndPoint, tokenCredential: new DefaultAzureCredential(options), GetClientOptions(setting));
            }
            else
            {
                client = new CosmosClient(connectionSettings.AccountEndPoint, connectionSettings.AccountKey, GetClientOptions(setting));
            }

            return client;
        }

        private CosmosClientOptions GetClientOptions<TCache>(ICosmosDbAccessAdapterInstanceConfiguration<TCache> settings)
        {
            var clientOptions = new CosmosClientOptions();

            switch (settings.Connection.ClientConnectionMode.Trim().ToLower())
            {
                case "httpgateway":
                    clientOptions.ConnectionMode = ConnectionMode.Gateway;
                    break;
                default:
                case "directtcp":
                    clientOptions.ConnectionMode = ConnectionMode.Direct;
                    break;
            }

            if (settings.UseReadOnlyConnection && settings.PreferredReadLocations != null && settings.PreferredReadLocations.Count > 0)
            {
                clientOptions.ApplicationPreferredRegions = settings.PreferredReadLocations.ToList();
            }

            return clientOptions;
        }

        private void ValidateConectionString(CosmosDbConnection cosmosDbConnectionString)
        {
            if (cosmosDbConnectionString == null)
            {
                Debugger.Log(1, "CosmosDbHandler", $"cosmosDbConnectionString is not valid");
                throw new ArgumentNullException("Cosmos db connection string is missing");
            }

            if (string.IsNullOrEmpty(cosmosDbConnectionString.AccountEndPoint))
                throw new ArgumentNullException("DocumentDb account endpoint is missing");

            if (!cosmosDbConnectionString.UseManagedIdentity)
            {
                if (string.IsNullOrEmpty(cosmosDbConnectionString.AccountKey))
                    throw new ArgumentNullException("DocumentDb AccountKey is missing");
            }

            if (string.IsNullOrEmpty(cosmosDbConnectionString.Database))
                throw new ArgumentNullException("DocumentDb Database name is missing");
        }


        public async Task<IEnumerable<T>> ReadAsync<T>(object setting, CancellationToken cancellation)
        {
            if (!(setting is CosmosDbQueryConfiguration cosmosDbQueryConfiguration))
            {
                Debugger.Log(1, "CosmosDbHandler", $"Read setting is not valid");
                return null;
            }

            ConcurrentBag<T> resultCollection = new ConcurrentBag<T>();

            var container = _client.GetContainer(_database, _collection);
            QueryRequestOptions options = GetQueryOptions(cosmosDbQueryConfiguration);

            using (FeedIterator<T> query = container.GetItemQueryIterator<T>(cosmosDbQueryConfiguration.Query, requestOptions: options))
            {
                while (query.HasMoreResults)
                {
                    Parallel.ForEach(await query.ReadNextAsync(), (item) =>
                    {
                        resultCollection.Add(item);
                    });
                }
            }

            return resultCollection;
        }

        QueryRequestOptions GetQueryOptions(CosmosDbQueryConfiguration cosmosDbQueryConfiguration)
        {
            QueryRequestOptions options = new QueryRequestOptions()
            {
                MaxConcurrency = -1,
                MaxItemCount = -1,
                MaxBufferedItemCount = -1
            };

            if (cosmosDbQueryConfiguration.UseIntegratedCache && cosmosDbQueryConfiguration.IntegratedCacheStalenessInMinutes > 0)
            {
                //double cacheStalenessInMilliseconds = (double)TimeSpan.FromMinutes(cosmosDbQueryConfiguration.IntegratedCacheStalenessInMinutes).TotalMilliseconds;

                //options.AddRequestHeaders = (h) =>
                //{
                //    h.Add("x-ms-dedicatedgateway-max-age", cacheStalenessInMilliseconds.ToString(CultureInfo.InvariantCulture));
                //};

                options.DedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions
                {
                    MaxIntegratedCacheStaleness = TimeSpan.FromMilliseconds(cosmosDbQueryConfiguration.IntegratedCacheStalenessInMinutes * 60 * 1000)
                };
            }

            if (!string.IsNullOrWhiteSpace(cosmosDbQueryConfiguration.PartionKey))
            {
                options.PartitionKey = new PartitionKey(cosmosDbQueryConfiguration.PartionKey);
            }

            return options;
        }

        public async Task WriteAsync<TIn, TWriteSetting>(TIn dataItem, TWriteSetting writeSettings = default, CancellationToken cancellation = default)
        {
            CosmosDbWriteSettings cosmosDbWriteSettings = writeSettings as CosmosDbWriteSettings;

            // this makes sure response is light weight and gets only header
            _client.ClientOptions.EnableContentResponseOnWrite = false;
            var container = _client.GetContainer(_database, _collection);

            switch (cosmosDbWriteSettings.Action)
            {
                case CosmosDbAction.Insert:
                    await container.CreateItemAsync(dataItem);
                    Debugger.Log(1, "CosmosDbHandler", $"Insertion of {nameof(dataItem)} is successful");
                    break;
                case CosmosDbAction.Upsert:
                    await container.UpsertItemAsync(dataItem);
                    Debugger.Log(1, "CosmosDbHandler", $"Upsert operation of {nameof(dataItem)} is successful");
                    break;
                case CosmosDbAction.Delete:
                    await container.DeleteItemAsync<TIn>(Convert.ToString(dataItem), new PartitionKey(Convert.ToString(cosmosDbWriteSettings.PartitionKeyValue)));
                    Debugger.Log(1, "CosmosDbHandler", $"Deletion of {nameof(dataItem)} is successful");
                    break;
                default:
                    break;
            }
        }
    }
}
