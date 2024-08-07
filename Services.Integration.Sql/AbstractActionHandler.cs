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
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Services.Core.Logging.Extensions;
using Services.Integration.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Services.Integration.Sql
{
    public abstract class AbstractActionHandler<TSvcRequest, TSvcResponse> : IExternalIntegrationAction
    {
        protected ISqlActionConfig _config;
        List<Type> _registeredResponseBuilders = null, _registeredOperationHandles = null;
        protected IConnectionManager _sqlConnectionManager = default;

        public AbstractActionHandler() => (_registeredResponseBuilders, _registeredOperationHandles) = (new List<Type>(), new List<Type>());

        async Task<object> IExternalIntegrationAction.ExecuteAsync<TIn, TConfig>(TIn input, TConfig config)
        {
            _config = (ISqlActionConfig)config;

            if (_config == default)
            {
                throw new ExternalIntegrationException("SqlActionConfig is null");
            }

            Stopwatch watch = new Stopwatch();

            try
            {
                var externalServiceRequest = GetRequest(input);
                if (externalServiceRequest == null)
                {
                    throw new ExternalIntegrationException($"{ServiceAction} request is null");
                }

                _sqlConnectionManager = new ConnectionManager(SqlSettings);

                if (OperationHandle == default)
                {
                    throw new ExternalIntegrationException($"{ServiceAction} client is null");
                }

                await _sqlConnectionManager.Establish();

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
                LogDependency(ServiceAction, "Sql", ActionName, watch.Elapsed, false);
                LogException(ex);
                throw;
            }
            finally
            {
                await _sqlConnectionManager.Close();
            }
        }

        private void LogException(Exception ex)
        {
            _config?.Logger?.LogException(ex);
        }


        private async Task<TSvcResponse> Execute(Stopwatch watch, TSvcRequest externalServiceRequest)
        {
            if (SqlSettings.EnableRequestResponseLogging)
            {
                LogEvent($"{ActionName} request", new KeyValuePair<string, string>("Request", Serialize(externalServiceRequest)));
            }

            LogTrace($"Invoking {ActionName}");

            watch.Start();
            var externalServiceResponse = await Invoke(externalServiceRequest, OperationHandle);
            watch.Stop();

            LogDependency(ServiceAction, "Sql", ActionName, watch.Elapsed, true);

            if (SqlSettings.EnableRequestResponseLogging)
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

            try
            {
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                    Formatting = Formatting.Indented,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                return JsonConvert.SerializeObject(obj, settings);
            }
            catch
            {
                return "Cannot be serialized";
            }
        }

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

        protected ISqlConfiguration SqlSettings => ServiceExecutionMetadata.Settings as ISqlConfiguration;

        protected abstract TSvcRequest GetRequest<TIn>(TIn input);  /// Concrete implmenetations reside inside the implementation modules
        protected abstract Task<TSvcResponse> Invoke(TSvcRequest request, ISqlOperation operationHandle);
        protected abstract string ActionName { get; }
        protected abstract string ServiceAction { get; }
        protected abstract bool NeedsResponseBuilder { get; }

        IResponseBuilder ResponseBuilder  /// Concrete implmenetations reside inside the implementation modules
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

        ISqlOperation _operationHandle = default;
        ISqlOperation OperationHandle
        {
            get
            {
                if (_operationHandle != default)
                {
                    return _operationHandle;
                }

                var registeredTypes = _config?.Cache?.Get<Dictionary<Type, Dictionary<string, Type>>>("RegisteredTypes");

                if (registeredTypes != null && registeredTypes.ContainsKey(typeof(ISqlOperation)))
                {
                    var actionType = registeredTypes[typeof(ISqlOperation)][ServiceAction];
                    if (actionType != null)
                    {
                        _operationHandle = (ISqlOperation)Activator.CreateInstance(actionType, _sqlConnectionManager, SqlSettings);
                        return _operationHandle;
                    }
                }

                if (!_registeredOperationHandles.Any())
                {
                    var type = typeof(ISqlOperation);
                    //Added workaround to skip Microsoft.Azure assembly
                    var types = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.FullName.ToLower().Contains("microsoft.azure"))
                        .SelectMany(x => x.GetTypes()).Where(p => type.IsAssignableFrom(p));

                    if (types.Any())
                    {
                        _registeredOperationHandles.AddRange(types.ToList());
                    }
                }

                foreach (var m in _registeredOperationHandles)
                {
                    var a = m.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(SqlOperationMetadataAttribute));

                    if (a == null)
                    {
                        continue;
                    }

                    if (a.ConstructorArguments[0].Value.ToString() == ((int)SqlSettings.CommandType).ToString())
                    {
                        _operationHandle = (ISqlOperation)Activator.CreateInstance(m, _sqlConnectionManager, SqlSettings);
                    }
                }

                return _operationHandle;
            }
        }
    }
}
