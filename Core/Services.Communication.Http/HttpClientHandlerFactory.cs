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
using Services.Core.Contracts;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Services.Communication.Http
{
    sealed class HttpClientHandlerFactory
    {
        internal static async Task<IHttpClientHandlerBase> GetClientHandler<TCache>(IHttpClientConfig config, TCache cache = default)
        {
            var attr = await GetAuthAttribute(config);
            if (attr is X509Certificate2 certificate)
            {
                return new CertificateClientHandler<TCache>(config, certificate, cache);
            }
            else if (attr is OAuthClientType oauth)
            {
                return new OAuthClientHandler<TCache>(config, oauth, cache);
            }
            else if (attr is NoAuthType noauth)
            {
                return new NoAuthClientHandler<TCache>(config, noauth, cache);
            }
            else
            {
                throw new NotSupportedException($"The authentication type {attr.GetType()} is not supported");
            }
        }

        private static async Task<object> GetAuthAttribute(IHttpClientConfig config)
        {
            if (config.AuthenticationCallback == null)
            {
                return new NoAuthType();
            }

            var auth = await config.AuthenticationCallback();
            if (auth == null)
            {
                throw new ArgumentNullException($"Authentication cannot be null");
            }

            if (auth is string @string)
            {
                return new OAuthClientType { BearerToken = @string };
            }

            else if (auth is X509Certificate2)
            {
                return auth;
            }

            return new object();
        }
    }
}
