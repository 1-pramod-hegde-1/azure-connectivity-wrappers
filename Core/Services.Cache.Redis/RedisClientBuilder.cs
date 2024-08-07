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
using Services.Cache.Contracts;
using StackExchange.Redis;

namespace Services.Cache.Redis
{
    sealed class RedisClientBuilder
    {
        IRedisCacheProviderConfiguration _config;

        internal RedisClientBuilder (IRedisCacheProviderConfiguration config)
        {
            _config = config;
        }

        internal IConnectionMultiplexer Build ()
        {
            return ConnectionMultiplexer.Connect(ConnectionString);
        }

        string ConnectionString
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_config.ConnectionString))
                {
                    return _config.ConnectionString;
                }

                return $"{_config.Host}:{Port};{Password}{IsSsl}abortConnect=False";
            }
        }

        int Port
        {
            get
            {
                if (_config.Port > 0)
                {
                    return _config.Port;
                }

                if (!string.IsNullOrWhiteSpace(_config.ConnectionString) || _config.Host.Equals("localhost"))
                {
                    return 6379;
                }
                else
                {
                    return 6380;
                }
            }
        }

        string IsSsl
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_config.ConnectionString) || _config.Host.Equals("localhost"))
                {
                    return "ssl=False;";
                }
                else
                {
                    return "ssl=True;";
                }
            }
        }

        string Password
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_config.ConnectionString) || _config.Host.Equals("localhost"))
                {
                    return string.Empty;
                }

                return $"password={_config.Password};";
            }
        }
    }
}
