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
using Services.Core.Logging.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Services.Integration.Core;
using Microsoft.Extensions.Caching.Memory;

namespace Services.Integration.Http
{
    public abstract class AbstractActionHandler<TSvcRequest, TSvcResponse> : IExternalIntegrationAction
    {
        protected IHttpActionConfig _config;
        List<Type> _registeredResponseBuilders = null;
        List<Type> _registeredSecurityProviders = null;

        public AbstractActionHandler()
        {
            _registeredResponseBuilders = new List<Type>();
            _registeredSecurityProviders = new List<Type>();
            AdditionalProperties = new List<object>();
        }

        async Task<object> IExternalIntegrationAction.ExecuteAsync<TIn, TConfig>(TIn input, TConfig config)
        {
            _config = (IHttpActionConfig)config;

            if (_config == default)
            {
                throw new ExternalIntegrationException("HttpActionConfig is null");
            }

            Stopwatch watch = new Stopwatch();

            try
            {
                var externalServiceRequest = GetRequest(input);
                if (externalServiceRequest == null)
                {
                    throw new ExternalIntegrationException($"{ServiceAction} request is null");
                }

                TSvcResponse externalServiceResponse = await Execute(watch, externalServiceRequest);

                if (!NeedsResponseBuilder)
                {
                    return externalServiceResponse;
                }

                var responseBuilder = ResponseBuilder;

                if (responseBuilder == null)
                {
                    return externalServiceResponse;
                }

                if (AdditionalProperties != null && AdditionalProperties.Any())
                {
                    responseBuilder.SetConfig(AdditionalProperties);
                }

                return responseBuilder.CreateResponse(input, externalServiceResponse);
            }
            catch(Exception ex)
            {
                watch.Stop();
                LogDependency(ServiceAction, "ExternalService", ActionName, watch.Elapsed, false);
                LogException(ex);
                throw;
            }
        }

        private void LogException(Exception ex)
        {
            _config?.Logger?.LogException(ex);
        }

        private async Task<TSvcResponse> Execute(Stopwatch watch, TSvcRequest externalServiceRequest)
        {
            if (ServiceSettings.EnableRequestResponseLogging)
            {
                LogEvent($"{ActionName} request", new KeyValuePair<string, string>("Request", Serialize(externalServiceRequest)));
            }

            LogTrace($"Invoking {ActionName}");

            watch.Start();
            var externalServiceResponse = await Invoke(externalServiceRequest);
            watch.Stop();

            LogDependency(ServiceAction, "ExternalService", ActionName, watch.Elapsed, true);

            if (ServiceSettings.EnableRequestResponseLogging)
            {
                LogEvent($"{ActionName} response", new KeyValuePair<string, string>("Response", Serialize(externalServiceResponse)));
            }

            return externalServiceResponse;
        }

        protected void LogTrace(string m)
        {
            _config.Logger?.LogTrace(m);
        }

        protected void LogEvent(string m, params KeyValuePair<string, string>[] properties)
        {
            var p = new Dictionary<string, object>();
            foreach (var x in properties)
            {
                p.Add(x.Key, x.Value);
            }
            _config.Logger?.LogEvent(m, additionalProperties: p);
        }

        protected void LogDependency(string dependency, string type, string command, TimeSpan timespent, bool success)
        {
            _config.Logger?.LogDependency(dependency, timespent, success, dependencyType: type, command: command);
        }

        string Serialize<T>(T obj)
        {
            if (obj == null)
                return null;

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.All,
                Formatting = Formatting.None,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            return JsonConvert.SerializeObject(obj, settings);
        }

        protected abstract TSvcRequest GetRequest<TIn>(TIn input);        
        protected abstract Task<TSvcResponse> Invoke(TSvcRequest request);
        protected abstract string ActionName { get; }
        protected abstract string ServiceAction { get; }
        protected List<object> AdditionalProperties { get; set; }
        protected abstract bool NeedsResponseBuilder { get; }

        IExternalIntegrationMetadata _serviceExecutionMetadata;
        protected IExternalIntegrationMetadata ServiceExecutionMetadata
        {
            get
            {
                if (_serviceExecutionMetadata == null)
                {
                    _serviceExecutionMetadata = _config.OnServiceInvoke(ServiceAction);
                }
                return _serviceExecutionMetadata;
            }
        }

        protected IExternalServiceConfiguration ServiceSettings => ServiceExecutionMetadata.Settings as IExternalServiceConfiguration;

        IResponseBuilder ResponseBuilder
        {
            get
            {
                var registeredTypes = _config?.Cache?.Get<Dictionary<Type, Dictionary<string, Type>>>("RegisteredTypes");

                if (registeredTypes != null && registeredTypes.ContainsKey(typeof(IResponseBuilder)))
                {
                    var actionType = registeredTypes[typeof(IResponseBuilder)][ServiceAction];
                    if (actionType != null)
                    {
                        return (IResponseBuilder)Activator.CreateInstance(actionType);
                    }
                }

                if (!_registeredResponseBuilders.Any())
                {
                    var type = typeof(IResponseBuilder);
                    var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(p => type.IsAssignableFrom(p));

                    if (types.Any())
                    {
                        _registeredResponseBuilders.AddRange(types.ToList());
                    }
                }

                foreach (var m in _registeredResponseBuilders)
                {
                    var a = m.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(ExternalIntegrationActionAttribute) && a.ConstructorArguments[0].Value.ToString() == ServiceAction);

                    if (a == null)
                    {
                        continue;
                    }

                    return (IResponseBuilder)Activator.CreateInstance(m);
                }

                return null;
            }
        }

        protected IMessageSecurityProvider MessageSecurityProvider
        {
            get
            {
                if (ServiceSettings.MessageEncryptionConfig == null)
                {
                    return null;
                }

                object secureObject = new object();

                AsyncContext.Run(async () =>
                {
                    secureObject = await ServiceSettings.MessageEncryptionConfig.AuthenticationCallback();
                });

                var registeredTypes = _config?.Cache?.Get<Dictionary<Type, Dictionary<string, Type>>>("RegisteredTypes");

                if (registeredTypes != null && registeredTypes.ContainsKey(typeof(IMessageSecurityProvider)))
                {
                    var actionType = registeredTypes[typeof(IMessageSecurityProvider)][ServiceAction];
                    if (actionType != null)
                    {
                        return (IMessageSecurityProvider)Activator.CreateInstance(actionType, args: new[] { secureObject });
                    }
                }

                if (!_registeredSecurityProviders.Any())
                {
                    var type = typeof(IMessageSecurityProvider);
                    var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(p => type.IsAssignableFrom(p));

                    if (types.Any())
                    {
                        _registeredSecurityProviders.AddRange(types.ToList());
                    }
                }

                foreach (var m in _registeredSecurityProviders)
                {
                    var a = m.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(ExternalIntegrationActionAttribute) && a.ConstructorArguments[0].Value.ToString() == ServiceAction);

                    if (a == null)
                    {
                        continue;
                    }                    

                    return (IMessageSecurityProvider)Activator.CreateInstance(m, args: new[] { secureObject });
                }

                return null;
            }
        }
    }
}
