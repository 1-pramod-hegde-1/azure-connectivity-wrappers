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
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Nito.AsyncEx;
using Services.Core.Logging.Extensions;
using Services.Data.AzureServiceBus;
using Services.Data.Common;
using Services.Integration.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Services.Integration.AzureServiceBus
{
    public abstract class AbstractActionHandler<TSvcRequest, TSvcResponse> : IExternalIntegrationAction
    {
        protected IAsbActionConfig _config;

        List<Type> _registeredResponseBuilders = null;
        List<Type> _registeredSecurityProviders = null;


        protected IMessageSecurityProvider MessageSecurityProvider
        {
            get
            {
                if (AsbSettings.MessageEncryptionConfig == null)
                {
                    return null;
                }

                if (!_registeredSecurityProviders.Any())
                {
                    Type type = typeof(IMessageSecurityProvider);
                    IEnumerable<Type> source = from p in AppDomain.CurrentDomain.GetAssemblies().SelectMany((Assembly x) => x.GetTypes())
                                               where type.IsAssignableFrom(p)
                                               select p;
                    if (source.Any())
                    {
                        _registeredSecurityProviders.AddRange(source.ToList());
                    }
                }

                foreach (var m in _registeredSecurityProviders)
                {
                    var a = m.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(ExternalIntegrationActionAttribute) && a.ConstructorArguments[0].Value.ToString() == ServiceAction);

                    if (a == null)
                    {
                        continue;
                    }

                    object secureObject = new object();

                    AsyncContext.Run(async () =>
                    {
                        secureObject = await AsbSettings.MessageEncryptionConfig.MessageEncryptionAuthCallback();
                    });

                    return (IMessageSecurityProvider)Activator.CreateInstance(m, args: new[] { secureObject });
                }

                return null;
            }
        }
        public AbstractActionHandler()
        {
            _registeredResponseBuilders = new List<Type>();
        }

        async Task<object> IExternalIntegrationAction.ExecuteAsync<TIn, TConfig>(TIn input, TConfig config)
        {
            _config = (IAsbActionConfig)config;

            if (_config == default)
            {
                throw new ExternalIntegrationException("AsbActionConfig is null");
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

                return responseBuilder.CreateResponse(input, externalServiceResponse);
            }
            catch (Exception ex)
            {
                watch.Stop();
                LogDependency(ServiceAction, "AzureServiceBus", ActionName, watch.Elapsed, false);
                LogException(ex);
                throw;
            }
        }

        private void LogException(Exception ex, string message = "")
        {
            _config?.Logger?.LogException(ex, message: message);
        }

        private async Task<TSvcResponse> Execute(Stopwatch watch, TSvcRequest externalServiceRequest)
        {
            if (AsbSettings.EnableRequestResponseLogging)
            {
                LogEvent($"{ActionName} request", new KeyValuePair<string, string>("Request", Serialize(externalServiceRequest)));
            }

            LogTrace($"Invoking {ActionName}");

            watch.Start();
            var externalServiceResponse = await Invoke(externalServiceRequest);
            watch.Stop();

            LogDependency(ServiceAction, "AzureServiceBus", ActionName, watch.Elapsed, true);

            if (AsbSettings.EnableRequestResponseLogging)
            {
                LogEvent($"{ActionName} response", new KeyValuePair<string, string>("Response", Serialize(externalServiceResponse)));
            }

            return externalServiceResponse;
        }

        protected async Task SendAsync<T>(T data, IAzureServiceBusMessageWriterSetting writerSetting, bool trySecondaryConnection = false)
        {
            try
            {
                UpdateWriterSetting(writerSetting);
                await TrySendAsync(data, writerSetting, trySecondaryConnection);
            }
            catch (Azure.Messaging.ServiceBus.ServiceBusException serviceBusException)
            {
                if (trySecondaryConnection)
                {
                    LogException(serviceBusException);
                    throw;
                }
                else
                {
                    switch (serviceBusException.Reason)
                    {
                        case Azure.Messaging.ServiceBus.ServiceBusFailureReason.QuotaExceeded:
                        case Azure.Messaging.ServiceBus.ServiceBusFailureReason.ServiceBusy:
                        case Azure.Messaging.ServiceBus.ServiceBusFailureReason.ServiceTimeout:
                        case Azure.Messaging.ServiceBus.ServiceBusFailureReason.ServiceCommunicationProblem:
                            LogException(serviceBusException, $"Transient error occurred in Asb. Reason: {serviceBusException.Reason}");
                            LogTrace("Resending the message using secondary Asb connection");
                            await SendAsync(data, writerSetting, true);
                            break;
                        case Azure.Messaging.ServiceBus.ServiceBusFailureReason.MessageSizeExceeded:
                        case Azure.Messaging.ServiceBus.ServiceBusFailureReason.MessagingEntityDisabled:
                        case Azure.Messaging.ServiceBus.ServiceBusFailureReason.MessagingEntityAlreadyExists:
                        case Azure.Messaging.ServiceBus.ServiceBusFailureReason.SessionCannotBeLocked:
                        case Azure.Messaging.ServiceBus.ServiceBusFailureReason.SessionLockLost:
                        case Azure.Messaging.ServiceBus.ServiceBusFailureReason.MessageLockLost:
                        case Azure.Messaging.ServiceBus.ServiceBusFailureReason.GeneralError:
                        case Azure.Messaging.ServiceBus.ServiceBusFailureReason.MessagingEntityNotFound:
                        case Azure.Messaging.ServiceBus.ServiceBusFailureReason.MessageNotFound:
                        default:
                            LogException(serviceBusException);
                            throw;
                    }
                }
            }
        }

        private void UpdateWriterSetting(IAzureServiceBusMessageWriterSetting writerSetting)
        {
            writerSetting.MessageProperties["TrackingId"] = _config.CorrelationId;
            if (AsbSettings.AdditionalProperties?.Any() == true)
            {
                foreach (var property in AsbSettings.AdditionalProperties)
                {
                    writerSetting.MessageProperties[property.Key] = property.Value;
                }
            }
        }

        private async Task TrySendAsync<T>(T data, IAzureServiceBusMessageWriterSetting writerSetting, bool trySecondaryConnection = false)
        {
            IDataAccessAdapter adapter = default;
            try
            {
                var adapterConfig = new ServiceBusMessageAdapterConfig(cache: _config.Cache, settings: AsbSettings, secondaryConnection: trySecondaryConnection)
                {
                    MessageHandlers = new[] { _config.ServiceBusMessageHandler }
                };
                adapter = await new AzureServiceBusSourceAdapterFactory<IMemoryCache>().CreateAsync(adapterConfig, null, CancellationToken.None);
                await adapter.WriteAsync(data, writerSetting, CancellationToken.None);
            }
            finally
            {
                adapter?.Dispose();
            }
        }

        protected async Task<IEnumerable<ServiceBusReceivedMessage>> ReadAsync(ServiceBusMessageReceiveOptions options, string namespaceOrConnectionString = "", bool useManagedIdentity = false)
        {
            IDataAccessAdapter adapter = default;
            try
            {
                ServiceBusMessageAdapterConfig adapterConfig = new ServiceBusMessageAdapterConfig(cache: _config.Cache, settings: AsbSettings)
                {
                    MessageHandlers = new[] { _config.ServiceBusMessageHandler }
                };

                if (!string.IsNullOrWhiteSpace(namespaceOrConnectionString))
                {
                    adapterConfig.Connection.UseManagedIdentity = useManagedIdentity;
                    if (useManagedIdentity)
                    {
                        adapterConfig.Connection.Namespace = namespaceOrConnectionString;
                    }
                    else
                    {
                        adapterConfig.Connection.ConnectionString = namespaceOrConnectionString;
                    }
                }

                adapter = await new AzureServiceBusSourceAdapterFactory<IMemoryCache>().CreateAsync(adapterConfig, null, CancellationToken.None);
                return await adapter.ReadAsync<ServiceBusReceivedMessage>(options, CancellationToken.None);
            }
            finally
            {
                adapter?.Dispose();
            }
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
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            return JsonConvert.SerializeObject(obj, settings);
        }

        protected abstract TSvcRequest GetRequest<TIn>(TIn input);
        protected abstract Task<TSvcResponse> Invoke(TSvcRequest request);
        protected abstract string ActionName { get; }
        protected abstract string ServiceAction { get; }
        protected abstract bool NeedsResponseBuilder { get; }

        IExternalIntegrationMetadata _asbActionMetadata;
        protected IExternalIntegrationMetadata AsbActionMetadata
        {
            get
            {
                if (_asbActionMetadata == null)
                {
                    _asbActionMetadata = _config.OnActionInvoke(ServiceAction);
                }
                return _asbActionMetadata;
            }
        }

        protected IAsbConfiguration AsbSettings 
        {
            get 
            {
                if(_config.EnableDynamicCallbackMessagingConfiguration) 
                {
                    return _config.DynamicMessagingConfiguration;
                }
                return AsbActionMetadata.Settings as IAsbConfiguration;
            }
        } 

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
                    //Added workaround to skip Microsoft.Azure assembly
                    var types = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.FullName.ToLower().Contains("microsoft.azure"))
                        .SelectMany(x => x.GetTypes()).Where(p => type.IsAssignableFrom(p));

                    if (types.Any())
                    {
                        _registeredResponseBuilders.AddRange(types.ToList());
                    }
                }

                foreach (var m in _registeredResponseBuilders)
                {
                    var a = m.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(ExternalIntegrationActionAttribute));

                    if (a == null)
                    {
                        continue;
                    }

                    if (a.ConstructorArguments[0].Value.ToString() == ServiceAction)
                    {
                        return (IResponseBuilder)Activator.CreateInstance(m);
                    }
                }

                return null;
            }
        }
    }
}
