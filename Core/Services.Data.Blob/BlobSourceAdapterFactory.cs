﻿// Copyright (c) [2022] Pramod Hegde
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

namespace Services.Data.Blob
{
    public class BlobSourceAdapterFactory<TCache> : DataAdapterFactoryBase, IDataAccessAdapterFactory<IBlobAccessAdapterInstanceConfiguration<TCache>>
    {
        public string Description
        {
            get { return "This adapter implements data access classes for CosmosDb"; }
        }

        public Task<IDataAccessAdapter> CreateAsync(IBlobAccessAdapterInstanceConfiguration<TCache> configuration, IDataAccessContext context, CancellationToken cancellation)
        {
            return Task.Factory.StartNew(() => Create(configuration), cancellation);
        }

        private IDataAccessAdapter Create(IBlobAccessAdapterInstanceConfiguration<TCache> configuration)
        {
            Ensure.NotNull("configuration", configuration);           

            return new BlobAccessAdapter<TCache>(CreateInstanceConfiguration(configuration));
        }

        private IBlobAccessAdapterInstanceConfiguration<TCache> CreateInstanceConfiguration(IBlobAccessAdapterInstanceConfiguration<TCache> configuration)
        {
            return new BlobSourceAdapterInstanceConfiguration<TCache>
            {
                Connection = configuration.Connection,                
                CacheManager = configuration.CacheManager,
                Retries = configuration.Retries,
                RetryInterval = configuration.RetryInterval,
                BlobHandlers = configuration.BlobHandlers
            };
        }
    }
}