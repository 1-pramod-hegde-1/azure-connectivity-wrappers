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
using Services.Core.Contracts;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Services.Cache.CacheHandler
{
    public sealed class CacheFactory
    {
        static CacheFactory _defaultInstance;
        IList<ICacheManager> _internalCacheStores;
        static readonly object _mlock = new object();

        public static CacheFactory Default
        {
            get
            {
                if (_defaultInstance == null)
                {
                    lock (_mlock)
                    {
                        _defaultInstance = new CacheFactory();
                    };
                }

                return _defaultInstance;
            }
        }

        private CacheFactory ()
        {
            Load();
        }

        void Load ()
        {
            var registeredStores = Core.Composition.Container.Default.GetAll<ICacheManager>();

            if (registeredStores == null || !registeredStores.Any())
            {
                throw LocalErrors.NoValidCacheStoreFound();
            }

            _internalCacheStores = registeredStores.ToList();
        }

        public ICacheManager GetHandle (CacheStore store)
        {
            if (store == CacheStore.None)
            {
                throw LocalErrors.NoValidCacheStoreFound();
            }

            return this[store];
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals (object obj)
        {
            return base.Equals(obj);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode ()
        {
            return base.GetHashCode();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString ()
        {
            return base.ToString();
        }

        ICacheManager this[CacheStore storeType]
        {
            get
            {
                foreach (var m in _internalCacheStores)
                {
                    var a = (StoreTypeAttribute)m.GetType().GetCustomAttributes(typeof(StoreTypeAttribute), false).FirstOrDefault();

                    if (a == null)
                    {
                        continue;
                    }

                    if (a.Store == storeType)
                    {
                        return m;
                    }
                }

                throw LocalErrors.NoValidCacheStoreFound();
            }
        }
    }
}
