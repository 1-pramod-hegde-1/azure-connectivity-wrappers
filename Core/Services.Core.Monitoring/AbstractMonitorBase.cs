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
using System.Threading.Tasks;

namespace Services.Core.Monitoring
{
    public abstract class AbstractMonitorBase<T, TWatchDog> : IDependentComponent
    {
        protected T _setting;
        protected TWatchDog _watchDog;
        protected List<DependentComponentHealth> _health;
        protected bool _needsCaching;
        protected ICacheManager _cacheManager;

        public AbstractMonitorBase ()
        {
            _health = new List<DependentComponentHealth>();
        }

        public abstract string Id { get; }

        public abstract Task<IEnumerable<DependentComponentHealth>> CheckHealth ();
        public abstract IDependentComponent CreatePart ();
        public abstract Task Initialize ();

        public virtual void Initialize (dynamic setting, bool needsCaching = false, ICacheManager cacheManager = null)
        {
            _setting = setting;
            _cacheManager = cacheManager;
            _needsCaching = needsCaching;
            Initialize();
        }
    }
}
