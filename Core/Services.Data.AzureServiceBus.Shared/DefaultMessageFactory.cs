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
using Services.Core.Composition;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Services.Data.AzureServiceBus
{
    public class DefaultMessageFactory<TCache>
    {
        readonly IAzureServiceBusAccessAdapterInstanceConfiguration<TCache> _configuration;

        IEnumerable<IMessagingHandler> _handlers;

        internal DefaultMessageFactory (IAzureServiceBusAccessAdapterInstanceConfiguration<TCache> configuration)
        {
            _configuration = configuration;
            LoadHandlers();           
        }

        private void LoadHandlers ()
        {
            if (_configuration.MessageHandlers != null && _configuration.MessageHandlers.Any())
            {
                _handlers = _configuration.MessageHandlers;
            }
            else
            {
                _handlers = Container.Default.GetAll<IMessagingHandler>();

                Ensure.NotNullOrEmpty("ImmutableMessageHandlers", _handlers);               
            }

            InitializeHandlers();
        }

        private void InitializeHandlers ()
        {
            foreach (var h in _handlers)
            {                          
                h.Initialize(_configuration);
            }
        }

        internal async Task WriteAsync<TIn,TSetting> (TIn dataItem, TSetting setting, CancellationToken cancellation)
        {
            await TargetHandler.WriteAsync(dataItem, setting, cancellation);
        }

        internal async Task<IEnumerable<TOut>> ReadAsync<TOut> (object setting, CancellationToken cancellation)
        {
            return await TargetHandler.ReadAsync<TOut>(setting, cancellation);
        }

        internal void Dispose ()
        {
            foreach (var h in _handlers)
            {
                h.Dispose();
            }

            _handlers = null;
        }

        IMessagingHandler TargetHandler
        {
            get
            {
                return _handlers.First(h => h.Type == _configuration.TargetType);
            }
        }
    }
}
