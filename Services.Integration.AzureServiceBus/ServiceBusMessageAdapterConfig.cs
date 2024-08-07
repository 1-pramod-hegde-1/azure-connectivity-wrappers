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
using Services.Data.AzureServiceBus;
using Services.Data.Common;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;

namespace Services.Integration.AzureServiceBus
{
    class ServiceBusMessageAdapterConfig : IAzureServiceBusAccessAdapterConfiguration<IMemoryCache>
    {
        public string Id { get; }
        public ImmutableMessageTargetTypes TargetType { get; }
        public string Topic { get; }
        public string Subscription { get; }
        public string Queue { get; }
        public AzureStorageLocationMode? LocationMode { get; }
        public ServiceBusConnection Connection { get; set; }
        public int? Retries { get; set; }
        public TimeSpan? RetryInterval { get; set; }
        public IMemoryCache CacheManager { get; set; }
        public IEnumerable<IMessagingHandler> MessageHandlers { get; set; }
        public bool EnableSharding { get; set; }
        public int MinimumPartitionIndex { get; set; }
        public int MaximumPartitionIndex { get; set; }

        public ServiceBusMessageAdapterConfig(IAsbConfiguration settings, IMemoryCache cache = null, string id = "DefaultMessageAdapterConfig", bool secondaryConnection = false)
        {
            Id = id;
            Connection = settings.AuthenticationConfig.AuthenticationCallback(secondaryConnection);
            CacheManager = cache;
            TargetType = (ImmutableMessageTargetTypes)Enum.Parse(typeof(ImmutableMessageTargetTypes), settings.ChannelType.ToString());

            if (TargetType == ImmutableMessageTargetTypes.Queue)
            {
                Queue = settings.ChannelIdentifier;
            }
            else
            {
                var channelIdentifierSplit = settings.ChannelIdentifier.Split(':');
                Topic = channelIdentifierSplit[0];
            }

            if (settings.EnableSharding) 
            {
                EnableSharding = settings.EnableSharding;
                MinimumPartitionIndex = settings.MinimumPartitionIndex;
                MaximumPartitionIndex = settings.MaximumPartitionIndex;
            }
        }
    }
}
