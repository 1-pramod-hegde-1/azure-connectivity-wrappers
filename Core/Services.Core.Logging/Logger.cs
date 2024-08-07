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
using System.Reflection;

namespace Services.Core.Logging
{
    public class Logger<T> : ILogger<T>
    {
        private readonly ILoggerFactory _loggerFactory = default;
        private IConcreateLoggerProvider _loggerProvider = default;

        public Logger(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        private string GetContext(Type type)
        {
            if (type.GetTypeInfo().IsGenericType)
            {
                var fullName = type.GetGenericTypeDefinition().FullName;

                var parts = fullName.Split('+');

                for (var i = 0; i < parts.Length; i++)
                {
                    var partName = parts[i];

                    var backTickIndex = partName.IndexOf('`');
                    if (backTickIndex >= 0)
                    {
                        partName = partName.Substring(0, backTickIndex);
                    }

                    parts[i] = partName;
                }

                return string.Join(".", parts);
            }
            else
            {
                var fullName = type.FullName;

                if (type.IsNested)
                {
                    fullName = fullName.Replace('+', '.');
                }

                return fullName;
            }
        }

        void ILogger.AddScope(ILoggerContext logContext)
        {
            foreach (var l in _loggerProvider?.Loggers)
            {
                l?.AddScope(logContext);
            }
        }

        void ILogger.Log<TLog>(LogLevel logLevel, TLog logValue, IDictionary<string, object> additionalProperties = null, Func<TLog, Exception, string> formatter = null)
        {
            foreach (var l in _loggerProvider?.Loggers)
            {
                l?.Log(logLevel, logValue, additionalProperties, formatter);
            }
        }

        void ILogger<T>.Setup()
        {
            _loggerProvider = _loggerFactory.DefaultBuilder.Build();
        }

        ILogger<T> ILogger<T>.AddProvider<TConfig>(IProviderConfiguration<TConfig> configuration)
        {
            _loggerFactory?.DefaultBuilder.Add(configuration);
            return this;
        }

        void ILogger.Dispose()
        {
            foreach (var l in _loggerProvider?.Loggers)
            {
                l?.Dispose();
            }
        }
    }
}