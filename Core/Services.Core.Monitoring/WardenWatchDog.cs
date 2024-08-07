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
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Warden;
using Warden.Core;
using Warden.Watchers;

namespace Services.Core.Monitoring
{
    public sealed class WardenWatchDog : WatchDogBase<WardenConfiguration.Builder, IWatcherCheck, IWardenCheckResult>
    {
        List<Expression<Action<Exception>>> _errorHooks;
        List<Expression<Action<IWardenCheckResult>>> _failureHooks;
        List<Expression<Action<IWatcherCheck>>> _startHooks;
        List<Expression<Action<IWardenCheckResult>>> _successHooks;

        public WardenWatchDog ()
        {
            _builder = WardenConfiguration.Create();
            _errorHooks = new List<Expression<Action<Exception>>>();
            _failureHooks = new List<Expression<Action<IWardenCheckResult>>>();
            _startHooks = new List<Expression<Action<IWatcherCheck>>>();
            _successHooks = new List<Expression<Action<IWardenCheckResult>>>();
        }

        internal override void AddWatcher<TWatcher> (TWatcher watcher)
        {
            IWatcher w = (IWatcher)watcher;

            if (w == null)
            {
                return;
            }

            _builder.AddWatcher(w);
        }

        internal override async Task ExecuteAsync ()
        {
            _builder.SetGlobalWatcherHooks(GetHooks());
            var b = _builder.SetIterationsCount(1).Build();
            await WardenInstance.Create(b).StartAsync();
        }

        private Action<WatcherHooksConfiguration.Builder> GetHooks ()
        {
            return (h) =>
            {
                if (_errorHooks.Any())
                {
                    h.OnError(_errorHooks.ToArray());
                }
                if (_failureHooks.Any())
                {
                    h.OnFailure(_failureHooks.ToArray());
                }
                if (_successHooks.Any())
                {
                    h.OnSuccess(_successHooks.ToArray());
                }
                if (_startHooks.Any())
                {
                    h.OnStart(_startHooks.ToArray());
                }
            };
        }

        internal override void OnError (params Expression<Action<Exception>>[] handlers)
        {
            _errorHooks.AddRange(handlers);
        }

        internal override void OnFailure (params Expression<Action<IWardenCheckResult>>[] handlers)
        {
            _failureHooks.AddRange(handlers);
        }

        internal override void OnStart (params Expression<Action<IWatcherCheck>>[] handlers)
        {
            _startHooks.AddRange(handlers);
        }

        internal override void OnSuccess (params Expression<Action<IWardenCheckResult>>[] handlers)
        {
            _successHooks.AddRange(handlers);
        }
    }
}
