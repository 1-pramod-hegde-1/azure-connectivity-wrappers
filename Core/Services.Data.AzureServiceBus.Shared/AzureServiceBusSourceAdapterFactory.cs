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
using Services.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Services.Data.AzureServiceBus
{
    public sealed class AzureServiceBusSourceAdapterFactory<TCache> : DataAdapterFactoryBase, IDataAccessAdapterFactory<IAzureServiceBusAccessAdapterConfiguration<TCache>>
    {
        public string Description
        {
            get { return "This adapter implements data access classes for AzureServiceBus"; }
        }

        public Task<IDataAccessAdapter> CreateAsync(IAzureServiceBusAccessAdapterConfiguration<TCache> configuration, IDataAccessContext context, CancellationToken cancellation)
        {
            return Task.Factory.StartNew(() => Create(configuration), cancellation);
        }

        private static IDataAccessAdapter Create(IAzureServiceBusAccessAdapterConfiguration<TCache> configuration)
        {
            Ensure.NotNull("configuration", configuration);

            if (configuration.Connection == null ||
                (!configuration.Connection.UseManagedIdentity && string.IsNullOrWhiteSpace(configuration.Connection.ConnectionString)) ||
                (configuration.Connection.UseManagedIdentity && string.IsNullOrWhiteSpace(configuration.Connection.Namespace)))
                throw LocalErrors.ConnectionStringMissing();

            return new AzureServiceBusAccessAdapter<TCache>(CreateInstanceConfiguration(configuration));
        }

        private static IAzureServiceBusAccessAdapterInstanceConfiguration<TCache> CreateInstanceConfiguration(IAzureServiceBusAccessAdapterConfiguration<TCache> configuration)
        {
            return new AzureServiceBusSourceAdapterInstanceConfiguration<TCache>
            {
                Connection = configuration.Connection,
                LocationMode = configuration.LocationMode,
                EnableSharding = configuration.EnableSharding,
                MaximumPartitionIndex = configuration.MaximumPartitionIndex,
                MinimumPartitionIndex = configuration.MinimumPartitionIndex,
                Id = configuration.Id,
                Queue = configuration.Queue,
                Subscription = configuration.Subscription,
                TargetType = configuration.TargetType,
                Topic = configuration.Topic,
                MessageHandlers = configuration.MessageHandlers,
                CacheManager = configuration.CacheManager,
                Retries = configuration.Retries,
                RetryInterval = configuration.RetryInterval
            };
        }
    }
}
