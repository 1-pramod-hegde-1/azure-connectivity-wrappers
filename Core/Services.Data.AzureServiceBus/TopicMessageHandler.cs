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
using Azure.Messaging.ServiceBus;
using Services.Core.Common;
using Services.Core.Contracts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Services.Data.AzureServiceBus
{
    [Export(typeof(ICompositionPart))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class TopicMessageHandler : IMessagingHandler
    {
        ServiceBusSender _sender;
        ServiceBusReceiver _receiver;
        ConcurrentDictionary<Type, dynamic> _cacheStores = new ConcurrentDictionary<Type, dynamic>();
        ImmutableMessageTargetTypes IMessagingHandler.Type => ImmutableMessageTargetTypes.Topic;
        bool markedForCleanup;

        string ICompositionPart.Id => "DefaultTopicMessageHandler";

        void IMessagingHandler.Dispose()
        {
            Dispose();
        }

        void Dispose()
        {
            if (markedForCleanup)
            {
                _sender?.CloseAsync();
                _receiver?.CloseAsync();
            }
            GC.SuppressFinalize(this);
            GC.Collect();
        }

        void IMessagingHandler.Initialize<TCache>(IAzureServiceBusAccessAdapterInstanceConfiguration<TCache> setting)
        {
            if (setting.CacheManager == null)
            {
                _sender = this.CreateSenderClient(setting);
                _receiver = this.CreateReceiverClient(setting);
                markedForCleanup = true;
            }
            else
            {
                ClientCacheHandler<TCache> _cache = GetClientCacheHandler(setting.CacheManager);
                InitializeSender(setting, _cache, $"{setting.Connection.Namespace}:{setting.Topic}:{setting.Subscription}:sender");
                InitializeReceiver(setting, _cache, $"{setting.Connection.Namespace}:{setting.Topic}:{setting.Subscription}:receiver");
            }
        }

        private void InitializeSender<TCache>(IAzureServiceBusAccessAdapterInstanceConfiguration<TCache> setting, ClientCacheHandler<TCache> _cache, string key)
        {
            if (_cache.Contains(key))
            {
                _sender = (ServiceBusSender)_cache.Get(key);
            }
            else
            {
                _sender = this.CreateSenderClient(setting);
                _cache.Insert(key, _sender);
            }
        }

        private void InitializeReceiver<TCache>(IAzureServiceBusAccessAdapterInstanceConfiguration<TCache> setting, ClientCacheHandler<TCache> _cache, string key)
        {
            if (_cache.Contains(key))
            {
                _receiver = (ServiceBusReceiver)_cache.Get(key);
            }
            else
            {
                _receiver = this.CreateReceiverClient(setting);
                _cache.Insert(key, _receiver);
            }
        }

        private ServiceBusSender CreateSenderClient<TCache>(IAzureServiceBusAccessAdapterInstanceConfiguration<TCache> setting)
        {
            ServiceBusClient serviceBusClient = CreateClientInternal(setting);

            var channelIdentifier = setting.Topic;

            if (setting.EnableSharding)
            {
                channelIdentifier += $"{GetPartitionIndex(setting.MinimumPartitionIndex, setting.MaximumPartitionIndex)}";
            }

            return serviceBusClient.CreateSender(channelIdentifier);
        }

        private ServiceBusReceiver CreateReceiverClient<TCache>(IAzureServiceBusAccessAdapterInstanceConfiguration<TCache> setting)
        {
            ServiceBusClient serviceBusClient = CreateClientInternal(setting);

            return serviceBusClient.CreateReceiver(setting.Topic, setting.Subscription);
        }

        private ClientCacheHandler<TCache> GetClientCacheHandler<TCache>(TCache cacheManager)
        {
            if (_cacheStores.ContainsKey(typeof(TCache)))
            {
                return _cacheStores[typeof(TCache)] as ClientCacheHandler<TCache>;
            }

            var type = typeof(ICacheHandler);
            var types = Assembly.GetAssembly(type).GetTypes().Where(p => type.IsAssignableFrom(p));

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
       

        private int GetPartitionIndex(int minimumPartitionIndex, int maximumPartitionIndex)
        {
            if (maximumPartitionIndex == minimumPartitionIndex)
            {
                return minimumPartitionIndex;
            }

            ++maximumPartitionIndex;

            int seed = (int)(DateTime.UtcNow.Ticks & 0xFFFFFFFF);
            var random = new Random(seed);

            int partition = random.Next(minimumPartitionIndex, maximumPartitionIndex);

            if (partition <= minimumPartitionIndex)
            {
                return minimumPartitionIndex;
            }
            else if (partition >= maximumPartitionIndex)
            {
                return maximumPartitionIndex;
            }
            else
            {
                return partition;
            }
        }


        private ServiceBusClient CreateClientInternal<TCache>(IAzureServiceBusAccessAdapterInstanceConfiguration<TCache> setting)
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

                return new ServiceBusClient($"{setting.Connection.Namespace}.servicebus.windows.net",
                    new DefaultAzureCredential(options),
                    options: new ServiceBusClientOptions
                    {
                        TransportType = ServiceBusTransportType.AmqpTcp,
                        RetryOptions = new ServiceBusRetryOptions
                        {
                            MaxRetries = 3,
                            Mode = ServiceBusRetryMode.Fixed,
                            MaxDelay = TimeSpan.FromSeconds(5),
                            Delay = TimeSpan.FromSeconds(2)
                        }
                    });

            }
            else
            {
                return new ServiceBusClient($"{setting.Connection.ConnectionString}", options: new ServiceBusClientOptions
                {
                    TransportType = ServiceBusTransportType.AmqpTcp,
                    RetryOptions = new ServiceBusRetryOptions
                    {
                        MaxRetries = 3,
                        Mode = ServiceBusRetryMode.Fixed,
                        MaxDelay = TimeSpan.FromSeconds(5),
                        Delay = TimeSpan.FromSeconds(2)
                    }
                });
            }
        }


        async Task<IEnumerable<T>> IMessagingHandler.ReadAsync<T>(object setting, CancellationToken cancellation)
        {
            var readSetting = setting as ServiceBusMessageReceiveOptions;
            if (readSetting == null || readSetting.Mode == ServiceBusMessageReceiveMode.Single)
            {
                return new[] { await _receiver.ReceiveMessageAsync() } as IEnumerable<T>;
            }
            else
            {
                var messages = await _receiver.ReceiveMessagesAsync(maxMessages: readSetting == null ? 100 : readSetting.MaxMessages);
                return messages.AsEnumerable() as IEnumerable<T>;
            }
        }

        async Task IMessagingHandler.WriteAsync<TIn, TWriteSetting>(TIn dataItem, TWriteSetting writeSettings, CancellationToken cancellation)
        {
            var message = dataItem is ServiceBusMessage ?
                          dataItem as ServiceBusMessage : CreateMessage(dataItem, writeSettings);

            await _sender.SendMessageAsync(message);
        }

        private ServiceBusMessage CreateMessage<TIn, TWriteSetting>(TIn dataItem, TWriteSetting setting)
        {
            return new MessageBuilder<TIn>(dataItem, (IAzureServiceBusMessageWriterSetting)setting).Message;
        }
    }
}
