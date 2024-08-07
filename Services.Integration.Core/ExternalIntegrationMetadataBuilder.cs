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
using System.Collections.Generic;
using System.Linq;

namespace Services.Integration.Core
{
    public sealed class ExternalIntegrationMetadataBuilder
    {
        ExternalIntegrationMetadataSet __set;

        public ExternalIntegrationMetadataBuilder()
        {
            if (__set == null)
            {
                __set = new ExternalIntegrationMetadataSet();
            }
        }

        public ExternalIntegrationMetadataBuilder(int capacity)
        {
            if (__set == null)
            {
                __set = new ExternalIntegrationMetadataSet(capacity);
            }
        }

        public ExternalIntegrationMetadataBuilder(KeyValuePair<string, IExternalIntegrationMetadata> serviceMetadata) : this()
        {
            if (__set != null)
            {
                __set.Add(serviceMetadata);
            }
        }

        public ExternalIntegrationMetadataBuilder(IEnumerable<IExternalIntegrationMetadata> serviceMetadataCollection) : this(serviceMetadataCollection.ToArray())
        {
        }

        public ExternalIntegrationMetadataBuilder(params IExternalIntegrationMetadata[] serviceMetadataCollection) : this()
        {
            if (__set != null)
            {
                foreach (var serviceMetadata in serviceMetadataCollection)
                {
                    __set.Add(serviceMetadata.ServiceType, serviceMetadata);
                }
            }
        }

        public ExternalIntegrationMetadataBuilder(string serviceType, IExternalIntegrationMetadata metadata) : this()
        {
            if (__set != null)
            {
                __set.Add(serviceType, metadata);
            }
        }

        public ExternalIntegrationMetadataBuilder Add(KeyValuePair<string, IExternalIntegrationMetadata> serviceMetadata)
        {
            if (!__set.ContainsKey(serviceMetadata.Key))
            {
                __set.Add(serviceMetadata);
            }

            return this;
        }

        public ExternalIntegrationMetadataBuilder Add(IExternalIntegrationMetadata serviceNode)
        {
            if (!__set.ContainsKey(serviceNode.ServiceType))
            {
                __set.Add(serviceNode.ServiceType, serviceNode);
            }

            return this;
        }

        public ExternalIntegrationMetadataBuilder Add(string serviceType, IExternalIntegrationMetadata metadata)
        {
            if (!__set.ContainsKey(serviceType))
            {
                __set.Add(serviceType, metadata);
            }

            return this;
        }

        public ExternalIntegrationMetadataSet ToMetadataSet()
        {
            return __set;
        }
    }
}
