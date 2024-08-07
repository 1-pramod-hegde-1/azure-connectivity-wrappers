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
using Azure;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Services.Core.Common;
using Services.Core.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Services.Data.Common.Extensions;

namespace Services.Data.Blob
{
    [Export(typeof(ICompositionPart))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class BlobHandler : IBlobHandler
    {
        public string Id => "DefaultBlobHandler";

        BlobServiceClient _client;
        ConcurrentDictionary<Type, dynamic> _cacheStores = new ConcurrentDictionary<Type, dynamic>();

        public void Dispose()
        {

        }

        public void Initialize<TCache>(IBlobAccessAdapterInstanceConfiguration<TCache> setting)
        {
            ValidateConectionString(setting.Connection);
            LoadClient(setting);
        }

        private void LoadClient<TCache>(IBlobAccessAdapterInstanceConfiguration<TCache> setting)
        {
            BlobConnection connectionSettings = setting.Connection;

            if (setting.CacheManager == null)
            {
                _client = CreateClient(setting);
            }
            else
            {
                ClientCacheHandler<TCache> _cache = GetClientCacheHandler(setting.CacheManager);

                if (_cache == null)
                {
                    _client = CreateClient(setting);
                }

                else
                {
                    string key = $"{connectionSettings.AccountName}";
                    if (_cache.Contains(key))
                    {
                        _client = (BlobServiceClient)_cache.Get(key);
                    }
                    else
                    {
                        _client = CreateClient(setting);
                        _cache.Insert(key, _client);
                    }
                }
            }
        }

        private ClientCacheHandler<TCache> GetClientCacheHandler<TCache>(TCache cacheManager)
        {
            if (_cacheStores.ContainsKey(typeof(TCache)))
            {
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
                                semaphore.Release();
                            }
                        }

                        return _cacheStores[typeof(TCache)] as ClientCacheHandler<TCache>;
                    }
                }
            }

            return default;
        }

        private BlobServiceClient CreateClient<TCache>(IBlobAccessAdapterInstanceConfiguration<TCache> setting)
        {
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
                    ExcludeInteractiveBrowserCredential = true
                };

                return new BlobServiceClient(new Uri($"https://{setting.Connection.AccountName}.blob.core.windows.net"), credential: new DefaultAzureCredential(options));
            }
            else
            {
                return new BlobServiceClient(setting.Connection.SecureKey);
            }
        }

        private void ValidateConectionString(BlobConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException("Blob connection is missing");
            }

            if (string.IsNullOrEmpty(connection.AccountName))
                throw new ArgumentNullException("Blob accountName is missing");

            if (!connection.UseManagedIdentity)
            {
                if (string.IsNullOrEmpty(connection.SecureKey))
                    throw new ArgumentNullException("Blob secureConnectionString is missing");
            }
        }

        public async Task<IEnumerable<T>> ReadAsync<T>(object setting, CancellationToken cancellation)
        {
            BlobQueryConfiguration queryConfiguration = setting as BlobQueryConfiguration;

            switch (queryConfiguration.QueryType)
            {
                case BlobQueryTypes.ReadBlobUri:
                    return ReturnBlobUri<T>(queryConfiguration);
                case BlobQueryTypes.ReadBlobContent:
                default:
                    return await ReadBlobContentAsync<T>(queryConfiguration);
            }
        }

        private IEnumerable<T> ReturnBlobUri<T>(BlobQueryConfiguration queryConfiguration)
        {
            var containerClient = _client.GetBlobContainerClient(queryConfiguration.Container.ToLower());
            var blobClient = containerClient.GetBlobClient(queryConfiguration.BlobName);
            return new[] { (T)Convert.ChangeType(blobClient.Uri.ToString(), typeof(T)) };
        }

        async Task<IEnumerable<T>> ReadBlobContentAsync<T>(BlobQueryConfiguration queryConfiguration)
        {
            BlobClient blobClient = default;

            if (!string.IsNullOrWhiteSpace(queryConfiguration.BlobUri))
            {
                var originalUri = new UriBuilder(queryConfiguration.BlobUri);
                var blobUriBuilder = new BlobUriBuilder(originalUri.Uri);
                blobClient = new BlobClient(blobUriBuilder.ToUri());
            }
            else
            {
                var containerClient = _client.GetBlobContainerClient(queryConfiguration.Container.ToLower());
                blobClient = containerClient.GetBlobClient(queryConfiguration.BlobName);
            }

            if (blobClient == default)
            {
                return default;
            }

            if (await blobClient.ExistsAsync())
            {
                var r = await blobClient.DownloadAsync();

                switch (queryConfiguration.FormattingMode)
                {
                    case ContentFormattingModes.None:
                        return CreateContentWithoutFormatting<T>(r);
                    case ContentFormattingModes.Decompress:
                    default:
                        return DownloadAndDecompress<T>(r);
                }
            }

            return default;
        }

        private IEnumerable<T> CreateContentWithoutFormatting<T>(Response<BlobDownloadInfo> rawContent)
        {
            if (typeof(T) == typeof(Stream))
            {
                return new[] { (T)Convert.ChangeType(rawContent.Value.Content, typeof(T)) };
            }
            else if (typeof(T) == typeof(byte[]))
            {
                return new[] { (T)Convert.ChangeType(ReadBytes(rawContent.Value.Content), typeof(T)) };
            }
            else
            {
                throw new Exception($"Ouput formatting of type {typeof(T)} is not supported");
            }
        }

        public byte[] ReadBytes(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        private IEnumerable<T> DownloadAndDecompress<T>(Response<BlobDownloadInfo> rawContent)
        {
            StreamReader reader = new StreamReader(rawContent.Value.Content);
            var downloadedContent = reader.ReadToEnd();

            return new[] { (T)Convert.ChangeType(downloadedContent.Decompress(), typeof(T)) };
        }

        public async Task WriteAsync<TIn, TWriteSetting>(TIn dataItem, TWriteSetting writeSettings = default, CancellationToken cancellation = default)
        {
            BlobWriteSettings blobWriteSettings = writeSettings as BlobWriteSettings;

            var containerClient = _client.GetBlobContainerClient(blobWriteSettings.Container.ToLower());

            switch (blobWriteSettings.Action)
            {
                case BlobWriteActions.InsertBlob:
                    await InsertBlob(containerClient, blobWriteSettings, dataItem);
                    break;
                case BlobWriteActions.RemoveBlob:
                    if (await containerClient.ExistsAsync())
                    {
                        await containerClient.DeleteBlobIfExistsAsync(blobWriteSettings.BlobName);
                    }
                    break;
                case BlobWriteActions.CreateContainer:
                    await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
                    break;
                case BlobWriteActions.DeleteContainer:
                    await containerClient.DeleteIfExistsAsync();
                    break;
                default:
                    break;
            }

        }

        private async Task InsertBlob<TIn>(BlobContainerClient containerClient, BlobWriteSettings settings, TIn dataItem)
        {
            if (dataItem == null || string.IsNullOrWhiteSpace(settings.BlobName))
            {
                return;
            }

            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
            var blobClient = containerClient.GetBlobClient(settings.BlobName);

            var content = (dataItem is string) ? dataItem as string : JsonConvert.SerializeObject(dataItem);
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(settings.CompressWhileStore ? content.Compress() : content);
            using (var msi = new MemoryStream(bytes))
            {
                await blobClient.UploadAsync(msi, transferOptions: new StorageTransferOptions
                {
                    MaximumConcurrency = 1000
                });
            }
        }
    }
}