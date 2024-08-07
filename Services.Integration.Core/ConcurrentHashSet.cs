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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Services.Integration.Core
{
    public class ConcurrentHashSet<T> : IEnumerable<T>
    {
        private readonly HashSet<T> hashSet = new HashSet<T>();
        private readonly ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();

        public void Add(T item)
        {
            rwLock.EnterWriteLock();
            try
            {
                hashSet.Add(item);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public void AddRange(T[] items)
        {
            rwLock.EnterWriteLock();
            try
            {
                foreach (var item in items)
                {
                    hashSet.Add(item);
                }                
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public bool Contains(T item)
        {
            rwLock.EnterReadLock();
            try
            {
                return hashSet.Contains(item);
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        public bool Remove(T item)
        {
            rwLock.EnterWriteLock();
            try
            {
                return hashSet.Remove(item);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public bool RemoveAll(T[] items)
        {
            rwLock.EnterWriteLock();
            try
            {
                var hasRemovedAllItems = true;

                foreach (var item in items)
                {
                    hasRemovedAllItems &= hashSet.Remove(item);
                }

                return hasRemovedAllItems;
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            rwLock.EnterReadLock();
            try
            {
                return hashSet.ToList().GetEnumerator();
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
