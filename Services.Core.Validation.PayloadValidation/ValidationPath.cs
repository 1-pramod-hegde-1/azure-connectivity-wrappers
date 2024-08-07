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
using System.Linq;

namespace Services.Core.Validation.PayloadValidation
{
    public sealed class ValidationPath : IList<ValidationNode>
    {
        private List<ValidationNode> __nodes;
        object _mutex = new object();

        public ValidationPath()
        {
            __nodes = new List<ValidationNode>();
        }

        public ValidationNode this[int index] { get => __nodes[index]; set => __nodes[index] = value; }

        public int Count => __nodes.Count;

        public bool IsReadOnly => false;

        public void Add(ValidationNode item)
        {
            lock (_mutex)
            {
                __nodes.Add(item);
            }
        }

        public void Clear()
        {
            lock (_mutex)
            {
                __nodes.Clear();
            }
        }

        public bool Contains(ValidationNode item)
        {
            return __nodes.Contains(item);
        }

        public void CopyTo(ValidationNode[] array, int arrayIndex)
        {
            throw new NotSupportedException();
        }

        public IEnumerator<ValidationNode> GetEnumerator()
        {
            return __nodes.GetEnumerator();
        }

        public int IndexOf(ValidationNode item)
        {
            return __nodes.IndexOf(item);
        }

        public void Insert(int index, ValidationNode item)
        {
            lock (_mutex)
            {
                __nodes.Insert(index, item);
            }
        }

        public bool Remove(ValidationNode item)
        {
            lock (_mutex)
            {
                return __nodes.Remove(item);
            }
        }

        public void RemoveAt(int index)
        {
            lock (_mutex)
            {
                __nodes.RemoveAt(index);
            }
        }

        public override string ToString()
        {
            if (__nodes.Any())
            {
                var visits = __nodes.Select(x => $"{(string.IsNullOrWhiteSpace(x.ParentId) ? "SequenceStart" : x.ParentId)}.{x.Id}");
                return string.Join("->", visits);
            }

            return string.Empty;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return __nodes.GetEnumerator();
        }

        public void AddRange(IList<ValidationNode> nodes)
        {
            lock (_mutex)
            {
                __nodes.AddRange(nodes);
            }
        }
    }
}
