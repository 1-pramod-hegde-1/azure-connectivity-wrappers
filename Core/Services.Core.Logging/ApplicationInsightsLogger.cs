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
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace Services.Core.Logging
{
    [SupportedLogLevels(LogLevel.Dependency, LogLevel.Exception, LogLevel.Request, LogLevel.Trace, LogLevel.Event, LogLevel.Debug, LogLevel.Critical, LogLevel.Error, LogLevel.Information, LogLevel.Warning)]
    public class ApplicationInsightsLogger : ILogger
    {
        TelemetryClient _telemetryClient;
        Dictionary<string, object> _property;
        readonly object _mutex = new object();
        TelemetryConfiguration _telemetryConfig;
        bool _markedForDeletion = false;

        public ApplicationInsightsLogger(IApplicationInsightsLogConfiguration config)
        {
            _telemetryConfig = CreateTelemetryConfig(config);
            
            if (config.EnableInitializers)
            {
                SetupInitializers(_telemetryConfig);
            }

            InitializeClient(_telemetryConfig, config);
            _property = new Dictionary<string, object>();
        }

        private static void SetupInitializers(TelemetryConfiguration telemetryConfig)
        {
            telemetryConfig.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());
            telemetryConfig.TelemetryInitializers.Add(new SequencePropertyInitializer());
            telemetryConfig.TelemetryInitializers.Add(new HttpDependenciesParsingTelemetryInitializer());
        }

        private void InitializeClient(TelemetryConfiguration telemetryConfig, IApplicationInsightsLogConfiguration config)
        {
            if (config.Cache == null)
            {
                _telemetryClient = new TelemetryClient(telemetryConfig) { InstrumentationKey = config.InstrumentationKey };
            }
            else
            {
                var key = $"{config.InstrumentationKey}-client";
                if (!config.Cache.TryGetValue(key, out TelemetryClient value))
                {
                    using (ICacheEntry cacheEntry = config.Cache.CreateEntry(key))
                    {
                        cacheEntry.SlidingExpiration = new TimeSpan(24, 0, 0);
                        value = new TelemetryClient(telemetryConfig) { InstrumentationKey = config.InstrumentationKey };
                        cacheEntry.SetValue(value);
                    }
                }

                _telemetryClient = value;
            }
        }

        private TelemetryConfiguration CreateTelemetryConfig(IApplicationInsightsLogConfiguration config)
        {
            var telemetryConfig = config.ActiveTelemetryConfiguration;

            if (telemetryConfig == null)
            {
                telemetryConfig = TelemetryConfiguration.CreateDefault();
                _markedForDeletion = true;
            }

            telemetryConfig.InstrumentationKey = config.InstrumentationKey;
            telemetryConfig.TelemetryChannel.DeveloperMode = config.DeveloperMode;

            return telemetryConfig;
        }

        void ILogger.AddScope(ILoggerContext logContext)
        {
            if (logContext == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(logContext.ExecutionContext))
            {
                AddValue(_property, new KeyValuePair<string, object>("x-application-context", logContext.ExecutionContext));
            }
            if (!string.IsNullOrWhiteSpace(logContext.CorrelationId))
            {
                AddValue(_property, new KeyValuePair<string, object>("x-ms-correlation-id", logContext.CorrelationId));
            }
            if (!string.IsNullOrWhiteSpace(logContext.SessionId))
            {
                AddValue(_property, new KeyValuePair<string, object>("x-ms-client-session-id", logContext.SessionId));
            }
            if (logContext.CommonProperties != null && logContext.CommonProperties.Any())
            {
                foreach (var p in logContext.CommonProperties)
                {
                    AddValue(_property, new KeyValuePair<string, object>(p.Key, JsonConvert.SerializeObject(p.Value)));
                }
            }
        }

        private void AddValue(Dictionary<string, object> collection, KeyValuePair<string, object> p)
        {
            lock (_mutex)
            {
                if (!collection.ContainsKey(p.Key))
                {
                    collection.Add(p.Key, p.Value);
                }
                else
                {
                    collection[p.Key] = p.Value;
                }
            };
        }

        void ILogger.Log<TLog>(LogLevel logLevel, TLog logValue, IDictionary<string, object> additionalProperties = null, Func<TLog, Exception, string> formatter = null)
        {
            if (!IsSupported(logLevel))
            {
                return;
            }

            switch (logLevel)
            {
                case LogLevel.Dependency:
                    TrackDependency(logValue as DependencyTelemetryLog, additionalProperties);
                    break;
                case LogLevel.Event:
                    TrackEvent(logValue as EventTelemetryLog, additionalProperties);
                    break;
                case LogLevel.Debug:
                    TrackEvent(logValue as string, additionalProperties);
                    break;
                case LogLevel.Exception:
                    TrackException(logValue as ExceptionTelemetryLog, additionalProperties);
                    break;
                case LogLevel.Trace:
                    TrackTrace(logValue as TraceTelemetryLog, additionalProperties);
                    break;
                case LogLevel.Information:
                case LogLevel.Critical:
                case LogLevel.Error:
                case LogLevel.Warning:
                    TraceTelemetryLog log = JsonConvert.SerializeObject(logValue);
                    log.SeverityLevel = (SeverityLevel)Enum.Parse(typeof(SeverityLevel), logLevel.ToString());
                    TrackTrace(log, additionalProperties);
                    break;
                default: break;
            }
        }

        private bool IsSupported(LogLevel logLevel)
        {
            var values = (ReadOnlyCollection<CustomAttributeTypedArgument>)(this.GetType().CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(SupportedLogLevelsAttribute)).ConstructorArguments)[0].Value;
            return values.Any(v => Convert.ToInt32(v.Value) == (int)logLevel);
        }

        internal void TrackEvent(EventTelemetryLog log, IDictionary<string, object> properties = null)
        {
            var eventLog = new EventTelemetry
            {
                Name = log.Name, //eventName
                Sequence = log.Sequence,
                Timestamp = log.Timestamp
            };

            foreach (var p in GetProperties(properties))
            {
                eventLog.Properties.Add(p.Key, Convert.ToString(p.Value));
            }

            _telemetryClient.TrackEvent(eventLog);
        }

        internal void TrackTrace(TraceTelemetryLog log, IDictionary<string, object> properties = null)
        {
            var traceLog = new TraceTelemetry
            {
                Timestamp = log.Timestamp,
                Message = log.Message,
                Sequence = log.Sequence,
                SeverityLevel = log.SeverityLevel
            };

            foreach (var p in GetProperties(properties))
            {
                traceLog.Properties.Add(p.Key, Convert.ToString(p.Value));
            }

            _telemetryClient.TrackTrace(traceLog);
        }

        internal void TrackException(ExceptionTelemetryLog log, IDictionary<string, object> properties = null)
        {
            var exceptionLog = new ExceptionTelemetry
            {
                Exception = log.Exception,
                SeverityLevel = log.SeverityLevel,
                Message = log.Message,
                Sequence = log.Sequence,
                Timestamp = log.Timestamp,
                ProblemId = log.ProblemId
            };

            foreach (var p in GetProperties(properties))
            {
                exceptionLog.Properties.Add(p.Key, Convert.ToString(p.Value));
            }

            _telemetryClient.TrackException(exceptionLog.Exception, exceptionLog.Properties);
        }

        internal void TrackDependency(DependencyTelemetryLog log, IDictionary<string, object> properties = null)
        {
            var dependency = new DependencyTelemetry
            {
                Data = log.Data, // command,
                Type = log.Type, // dependencyType,
                Id = log.Id, // id,
                Name = log.Name, // dependencyName,
                Duration = log.Duration, // duration,
                Success = log.Success, // success,
                ResultCode = log.ResultCode // resultCode
            };


            if (_property != null)
            {
                foreach (var p in GetProperties(properties))
                {
                    dependency.Properties.Add(p.Key, Convert.ToString(p.Value));
                }
            }

            _telemetryClient.TrackDependency(dependency);
        }

        private IDictionary<string, object> GetProperties(IDictionary<string, object> additionalProperties = null)
        {
            if (!_property.Any() && additionalProperties == null)
            {
                return null;
            }

            if (additionalProperties == null || !additionalProperties.Any())
            {
                return _property;
            }


            var newpropertylist = new Dictionary<string, object>();
            foreach (var p in additionalProperties)
            {
                AddValue(newpropertylist, new KeyValuePair<string, object>(p.Key, JsonConvert.SerializeObject(p.Value)));
            }

            foreach (var p in _property)
            {
                AddValue(newpropertylist, p);
            }

            return newpropertylist;
        }

        void ILogger.Dispose()
        {
            _telemetryClient?.Flush();
            _property.Clear();

            if (_markedForDeletion)
            {
                _telemetryConfig?.Dispose();
                _telemetryConfig = null;
            }
            
            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }
}