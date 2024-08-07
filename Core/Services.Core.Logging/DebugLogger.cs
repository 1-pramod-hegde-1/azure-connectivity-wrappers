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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Services.Core.Logging
{
    [SupportedLogLevels(LogLevel.Debug)]
    public class DebugLogger : ILogger
    {
        Dictionary<string, string> _property;
        readonly object _mutex = new object();
        IDebugLogConfiguration _config;

        public DebugLogger(IDebugLogConfiguration config)
        {
            _config = config;
            _property = new Dictionary<string, string>();
        }

        void ILogger.AddScope(ILoggerContext logContext)
        {
            if (logContext == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(logContext.ExecutionContext))
            {
                AddValue(_property, new KeyValuePair<string, string>("x-application-context", logContext.ExecutionContext));
            }
            if (!string.IsNullOrWhiteSpace(logContext.CorrelationId))
            {
                AddValue(_property, new KeyValuePair<string, string>("x-ms-correlation-id", logContext.CorrelationId));
            }
            if (!string.IsNullOrWhiteSpace(logContext.SessionId))
            {
                AddValue(_property, new KeyValuePair<string, string>("x-ms-client-session-id", logContext.SessionId));
            }
            if (logContext.CommonProperties != null && logContext.CommonProperties.Any())
            {
                foreach (var p in logContext.CommonProperties)
                {
                    AddValue(_property, new KeyValuePair<string, string>(p.Key, JsonConvert.SerializeObject(p.Value)));
                }
            }
        }

        private void AddValue(Dictionary<string, string> collection, KeyValuePair<string, string> p)
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

            Debug.WriteLine(logValue, logLevel.ToString());
        }

        private bool IsSupported(LogLevel logLevel)
        {
            var values = (ReadOnlyCollection<CustomAttributeTypedArgument>)(this.GetType().CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(SupportedLogLevelsAttribute)).ConstructorArguments)[0].Value;
            return values.Any(v => Convert.ToInt32(v.Value) == (int)logLevel);
        }

        void ILogger.Dispose()
        {
            _property.Clear();
            _property = null;
            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }
}