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
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace Services.Communication.Http
{
    sealed class CertificateClientHandler<TCache> : HttpClientHandlerBase<X509Certificate2, TCache>
    {
        internal CertificateClientHandler(IHttpClientConfig config, X509Certificate2 authResponse, TCache cacheManager) : base(config, authResponse, cacheManager) { }

        internal override bool NeedsHttpHandler => true;

        internal override HttpClientHandler WebHandler => GetInternalHandler();

        private HttpClientHandler GetInternalHandler()
        {
            lock (_mutext)
            {
                HttpClientHandler clientHandler = new HttpClientHandler
                {
                    ClientCertificateOptions = ClientCertificateOption.Manual
                };
                clientHandler.ClientCertificates.Add(_authResponse);
                return clientHandler;
            }
        }

        public override HttpClient GetClient()
        {
            lock (_mutext)
            {
                CreateClient();
            }

            return _client;
        }

        internal override void Decorate()
        {
            lock (_mutext)
            {
                base.Decorate();
            }
        }
    }
}
