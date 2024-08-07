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
using System;
using System.Collections;
using System.Collections.Generic;

namespace Services.Integration.Core
{
    public sealed class ExternalIntegrationMetadataSet : IDictionary<string, IExternalIntegrationMetadata>
    {
        private Dictionary<string, IExternalIntegrationMetadata> __internalItemCollection;

        public ExternalIntegrationMetadataSet()
        {
            __internalItemCollection = new Dictionary<string, IExternalIntegrationMetadata>();
        }

        public ExternalIntegrationMetadataSet(int capacity)
        {
            __internalItemCollection = new Dictionary<string, IExternalIntegrationMetadata>(capacity);
        }

        public IExternalIntegrationMetadata this[string key]
        {
            get { return __internalItemCollection[key]; }
            set { __internalItemCollection.Add(key, value); }
        }

        public ICollection<string> Keys => __internalItemCollection.Keys;

        public ICollection<IExternalIntegrationMetadata> Values => __internalItemCollection.Values;

        public int Count => __internalItemCollection.Count;

        public bool IsReadOnly => false;

        public void Add(string key, IExternalIntegrationMetadata value)
        {
            __internalItemCollection.Add(key, value);
        }

        public void Add(KeyValuePair<string, IExternalIntegrationMetadata> item)
        {
            __internalItemCollection.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            __internalItemCollection.Clear();
        }

        public bool Contains(KeyValuePair<string, IExternalIntegrationMetadata> item)
        {
            return __internalItemCollection.ContainsKey(item.Key) && __internalItemCollection.ContainsValue(item.Value);
        }

        public bool ContainsKey(string key)
        {
            return __internalItemCollection.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<string, IExternalIntegrationMetadata>[] array, int arrayIndex)
        {
            throw new NotSupportedException();
        }

        public IEnumerator<KeyValuePair<string, IExternalIntegrationMetadata>> GetEnumerator()
        {
            return __internalItemCollection.GetEnumerator();
        }

        public bool Remove(string key)
        {
            return __internalItemCollection.Remove(key);
        }

        public bool Remove(KeyValuePair<string, IExternalIntegrationMetadata> item)
        {
            return __internalItemCollection.Remove(item.Key);
        }

        public bool TryGetValue(string key, out IExternalIntegrationMetadata value)
        {
            return __internalItemCollection.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return __internalItemCollection.GetEnumerator();
        }
    }
}
