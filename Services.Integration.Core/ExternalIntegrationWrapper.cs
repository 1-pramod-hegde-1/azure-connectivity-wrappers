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
using System.Reflection;
using System.Threading.Tasks;

namespace Services.Integration.Core
{
    public sealed class ExternalIntegrationWrapper : IExternalIntegrationModule
    {
        List<Type> _registeredActionTypes = null;

        public ExternalIntegrationWrapper()
        {
            _registeredActionTypes = new List<Type>();
        }

        public string Id => nameof(ExternalIntegrationWrapper);

        async Task<object> IExternalIntegrationModule.ExecuteAsync<TConfig, TIn>(TIn input, TConfig config, string externalAction)
        {
            var action = this[externalAction];
            return await action.ExecuteAsync(input, config);
        }

        IExternalIntegrationAction this[string action]
        {
            get
            {
                if (!_registeredActionTypes.Any())
                {
                    var type = typeof(IExternalIntegrationAction);
                    var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(p => type.IsAssignableFrom(p));

                    if (types.Any())
                    {
                        _registeredActionTypes.AddRange(types.ToList());
                    }
                }

                foreach (var m in _registeredActionTypes)
                {
                    var a = m.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(ExternalIntegrationActionAttribute) && a.ConstructorArguments[0].Value.ToString() == action);

                    if (a == null)
                    {
                        continue;
                    }

                    return (IExternalIntegrationAction)Activator.CreateInstance(m);
                }

                return null;
            }
        }
    }
}
