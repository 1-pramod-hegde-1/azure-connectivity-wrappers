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
using Services.Core.Contracts;
using Services.Data.Common;
using Microsoft.ServiceBus.Messaging;
using System;
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
    public class TopicBrokeredMessageHandler : IMessagingHandler
    {
        TopicClient _client;

        ImmutableMessageTargetTypes IMessagingHandler.Type => ImmutableMessageTargetTypes.TopicWithBrokeredMessageSupport;

        string ICompositionPart.Id => "DefaultBrokeredMessageHandler";

        void IMessagingHandler.Dispose()
        {
            //if (_client != null)
            //{
            //    Task.Run(async () => await _client.CloseAsync());
            //}
        }

        void IMessagingHandler.Initialize<TCache>(IAzureServiceBusAccessAdapterInstanceConfiguration<TCache> setting)
        {
            if (setting.CacheManager == null)
            {
                _client = CreateClient(setting);
            }
            else
            {
                ClientCacheHandler<TCache> _cache = GetClientCacheHandler(setting.CacheManager);

                string key = $"{setting.Connection.Namespace}:{setting.Topic}";
                if (_cache.Contains(key))
                {
                    _client = (TopicClient)_cache.Get(key);
                }
                else
                {
                    _client = CreateClient(setting);
                    _cache.Insert(key, _client);
                }

            }
        }

        private ClientCacheHandler<TCache> GetClientCacheHandler<TCache>(TCache cacheManager)
        {
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
                        return (ClientCacheHandler<TCache>)Activator.CreateInstance(m, cacheManager);
                    }
                }
            }

            return default;
        }

        private TopicClient CreateClient<TCache>(IAzureServiceBusAccessAdapterInstanceConfiguration<TCache> setting) => TopicClient.CreateFromConnectionString(setting.Connection.ConnectionString, setting.Topic);

        Task<IEnumerable<T>> IMessagingHandler.ReadAsync<T>(object setting, CancellationToken cancellation)
        {
            throw new NotSupportedException();
        }

        async Task IMessagingHandler.WriteAsync<TIn, TWriteSetting>(TIn dataItem, TWriteSetting writeSettings, CancellationToken cancellation)
        {
            var message = dataItem is BrokeredMessage ?
                          dataItem as BrokeredMessage : CreateMessage(dataItem, writeSettings);

            await _client.SendAsync(message);
        }

        private BrokeredMessage CreateMessage<TIn, TWriteSetting>(TIn dataItem, TWriteSetting setting)
        {
            return new TopicBrokeredMessageBuilder<TIn>(dataItem, (IAzureServiceBusMessageWriterSetting)setting).Message;
        }
    }
}
