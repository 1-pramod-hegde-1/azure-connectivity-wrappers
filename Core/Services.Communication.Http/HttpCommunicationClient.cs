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
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Services.Core.Common;

namespace Services.Communication.Http
{
    [Export(typeof(ICompositionPart))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public sealed class HttpCommunicationClient : ICommunicationClient
    {
        private HttpClient _httpClient;

        private IHttpClientHandlerBase _handler;

        private readonly ConcurrentDictionary<string, string> _requestHeaders = new ConcurrentDictionary<string, string>();

        public string Id => "DefaultCommunicationHandler";

        readonly object _mutex = new object();

        public string this[string key]
        {
            get
            {
                if (_requestHeaders.ContainsKey(key))
                    return _requestHeaders[key];
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="baseUri"></param>
        /// <param name="cache"></param>
        /// <param name="authCallback"></param>
        /// <param name="timeout"></param>
        public void ConfigureClient<TCache>(string baseUri, AuthenticationCallback authCallback = null, TCache cache = default, TimeSpan timeout = default)
        {
            timeout = timeout == default ? new TimeSpan(0, 0, 100) : timeout;

            var task = Task.Run(async () =>
            {
                _handler = await HttpClientHandlerFactory.GetClientHandler(new GenericHttpClientConfig
                {
                    BaseUri = baseUri,
                    AuthenticationCallback = authCallback,
                    ConnectionTimeout = timeout
                }, cache);
            });

            task.Wait();
        }

        public async Task<T> GetAsync<T>(string resourceUri)
        {
            return await HttpProxy<T>(resourceUri, HttpMethod.Get, null);
        }

        public async Task PostAsync(string resourceUri, object content)
        {
            string contentString = JsonConvert.SerializeObject(content);
            await HttpProxy(resourceUri, HttpMethod.Post, contentString);
        }

        public async Task<T> PostAsync<T>(string resourceUri, object content)
        {
            string contentString = JsonConvert.SerializeObject(content);
            return await HttpProxy<T>(resourceUri, HttpMethod.Post, contentString);
        }

        public async Task PatchAsync(string resourceUri, object content)
        {
            string contentString = JsonConvert.SerializeObject(content);
            await HttpProxy(resourceUri, new HttpMethod("PATCH"), contentString);
        }

        public async Task<T> PatchAsync<T>(string resourceUri, object content)
        {
            string contentString = JsonConvert.SerializeObject(content);
            return await HttpProxy<T>(resourceUri, new HttpMethod("PATCH"), contentString);
        }

        public async Task DeleteAsync(string resourceUri, object content)
        {
            string contentString = JsonConvert.SerializeObject(content);
            await HttpProxy(resourceUri, new HttpMethod("DELETE"), contentString);
        }

        public async Task<T> DeleteAsync<T>(string resourceUri, object content)
        {
            string contentString = JsonConvert.SerializeObject(content);
            return await HttpProxy<T>(resourceUri, new HttpMethod("DELETE"), contentString);
        }

        public async Task PutAsync(string resourceUri, object content)
        {
            string contentString = JsonConvert.SerializeObject(content);
            await HttpProxy(resourceUri, HttpMethod.Put, contentString);
        }

        public async Task<T> PutAsync<T>(string resourceUri, object content)
        {
            string contentString = JsonConvert.SerializeObject(content);
            return await HttpProxy<T>(resourceUri, HttpMethod.Put, contentString);
        }

        private async Task HttpProxy(string resourceUri, HttpMethod httpMethod, string jsonString)
        {
            Ensure.NotNull(nameof(_handler), _handler);

            _httpClient = _handler.GetClient();
            Ensure.NotNull(nameof(_httpClient), _httpClient);

            string newUri = new string(_httpClient.BaseAddress.AbsoluteUri.ToCharArray());

            if (!string.IsNullOrWhiteSpace(resourceUri))
            {
                newUri = newUri + resourceUri;
            }

            var newReq = GetHttpRequest(newUri, httpMethod, jsonString);

            var cancellationToken = new CancellationTokenSource();
            using (var cts = GetCancellationTokenSource(newReq, cancellationToken.Token))
            {
                try
                {
                    var response = await _httpClient.SendAsync(newReq, cts.Token);

                    Ensure.NotNull(nameof(response), response);
                    if (response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.PartialContent || response.StatusCode == System.Net.HttpStatusCode.Created)
                        {
                            // do nothing. upload successful.
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                        {
                            throw new NoHttpContentException(response.ReasonPhrase);
                        }
                        else
                        {
                            throw new HttpStatusCodeException("Communication response is successful with Invalid content", response);
                        }
                    }
                    else
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                            response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout ||
                            response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                            response.StatusCode == System.Net.HttpStatusCode.BadGateway)
                        {
                            throw new RetriableHttpException((int)response.StatusCode, response.ReasonPhrase);
                        }
                        else
                        {
                            throw new HttpStatusCodeException("Communication response is not successful", response);
                        }
                    }
                }
                catch (OperationCanceledException oce) when (!cts.IsCancellationRequested)
                {
                    throw new HttpCommunicationTimeoutException(nameof(OperationCanceledException), oce);
                }
                catch (Exception ex) when (!(ex is NoHttpContentException || ex is HttpStatusCodeException || ex is RetriableHttpException))
                {
                    throw new HttpCommunicationException("Http service communication error", ex);
                }
            }
        }

        private CancellationTokenSource GetCancellationTokenSource(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var timeout = _httpClient.Timeout;
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                // No need to create a CTS if there's no timeout
                return null;
            }
            else
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeout);
                return cts;
            }
        }


        private async Task<T> HttpProxy<T>(string resourceUri, HttpMethod httpMethod, string jsonString)
        {
            Ensure.NotNull(nameof(_handler), _handler);

            _httpClient = _handler.GetClient();
            Ensure.NotNull(nameof(_httpClient), _httpClient);

            dynamic result;
            string newUri = new string(_httpClient.BaseAddress.AbsoluteUri.ToCharArray());

            if (!string.IsNullOrWhiteSpace(resourceUri))
            {
                newUri = newUri + resourceUri;
            }

            var newReq = GetHttpRequest(newUri, httpMethod, jsonString);

            var cancellationToken = new CancellationTokenSource();
            using (var cts = GetCancellationTokenSource(newReq, cancellationToken.Token))
            {
                try
                {
                    var response = await _httpClient.SendAsync(newReq, cts.Token);

                    if (typeof(T) == typeof(HttpResponseMessage))
                    {
                        result = response;
                        return result;
                    }

                    Ensure.NotNull(nameof(response), response);
                    if (response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.PartialContent || response.StatusCode == System.Net.HttpStatusCode.Created)
                        {
                            var serializedResponse = await response.Content.ReadAsStreamAsync();

                            if (typeof(T) == typeof(string))
                            {
                                result = await DeserializeToString<T>(serializedResponse);
                            }
                            else
                            {
                                result = Deserialize<T>(serializedResponse);
                            }
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                        {
                            throw new NoHttpContentException(response.ReasonPhrase);
                        }
                        else
                        {
                            throw new HttpStatusCodeException("Communication response is successful with Invalid content", response);
                        }
                    }
                    else
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                            response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout ||
                            response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                            response.StatusCode == System.Net.HttpStatusCode.BadGateway)
                        {
                            throw new RetriableHttpException((int)response.StatusCode, response.ReasonPhrase);
                        }
                        else
                        {
                            throw new HttpStatusCodeException("Communication response is not successful", response);
                        }
                    }
                }
                catch (OperationCanceledException oce) when (!cts.IsCancellationRequested)
                {
                    throw new HttpCommunicationTimeoutException(nameof(OperationCanceledException), oce);
                }
                catch (Exception ex) when (!(ex is NoHttpContentException || ex is HttpStatusCodeException || ex is RetriableHttpException))
                {
                    throw new HttpCommunicationException("Http service communication error.", ex);
                }
            }

            return result;
        }

        async Task<string> DeserializeToString<T>(Stream stream)
        {
            if (stream == null || !stream.CanRead)
            {
                return default;
            }

            using (var sr = new StreamReader(stream))
            {
                return await sr.ReadToEndAsync();
            }
        }

        T Deserialize<T>(Stream stream)
        {
            if (stream == null || !stream.CanRead)
            {
                return default;
            }

            using (var streamReader = new StreamReader(stream))
            {
                using (var textReader = new JsonTextReader(streamReader))
                {
                    var js = new JsonSerializer();
                    return js.Deserialize<T>(textReader);
                }
            }
        }

        /// <summary>
        /// Get HTTP request
        /// </summary>
        /// <param name="req">request message</param>
        /// <param name="newUri">new forwarding URI</param>
        /// <returns>HTTP response message</returns>
        private HttpRequestMessage GetHttpRequest(string newUri, HttpMethod httpMethod, string content)
        {
            UriBuilder targetUrlBuilder = new UriBuilder(newUri);

            HttpRequestMessage clone = new HttpRequestMessage(httpMethod, targetUrlBuilder.Uri);

            if (!string.IsNullOrEmpty(content) && (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put || httpMethod == HttpMethod.Delete || httpMethod.Method == "PATCH"))
            {
                clone.Content = new StringContent(content, Encoding.UTF8, "application/json");
            }

            foreach (var header in _requestHeaders)
            {
                if (clone.Headers.Contains(header.Key))
                {
                    continue;
                }

                lock (_mutex)
                {
                    clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            clone.Headers.Host = new Uri(newUri).Authority;

            clone.Properties["RequestTimeout"] = _httpClient.Timeout;

            return clone;
        }

        public void AddHeader(string name, string value)
        {
            _requestHeaders.AddOrUpdate(name, value, (key, oldValue) => value);
        }

		public void RemoveHeader(string name)
		{
			_requestHeaders.TryRemove(name, out string value);
		}

		public void ClearHeader()
		{
			_requestHeaders.Clear();
		}
	}
}
