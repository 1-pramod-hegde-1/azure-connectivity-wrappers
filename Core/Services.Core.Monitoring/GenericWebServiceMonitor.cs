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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Warden;
using Warden.Watchers;
using Warden.Watchers.Web;

namespace Services.Core.Monitoring
{
    [Export(typeof(ICompositionPart))]
    [ComponentType(DependentComponentTypes.WebService)]
    [ComponentType(DependentComponentTypes.WebApi)]
    [ComponentType(DependentComponentTypes.WcfService)]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public sealed class GenericWebServiceMonitor : AbstractMonitorBase<IHttpServiceMonitorConfig, WardenWatchDog>
    {
        public override string Id => "GenericWebServiceHealthMonitor";

        public override async Task<IEnumerable<DependentComponentHealth>> CheckHealth ()
        {
            await _watchDog.ExecuteAsync();
            return _health;
        }

        public override IDependentComponent CreatePart ()
        {
            return new GenericWebServiceMonitor();
        }

        IWatcher Watcher
        {
            get
            {
                IWatcher w = null;

                if (_needsCaching && _cacheManager != null)
                {
                    var k = _setting.Url;
                    if (_cacheManager.Contains(k))
                    {
                        w = (IWatcher)_cacheManager.Get(k);
                    }
                    else
                    {
                        w = GetWatcher();
                        _cacheManager.Insert(k, w, new DefaultCacheItemPolicy());
                    }
                }
                else
                {
                    w = GetWatcher();
                }

                return w;
            }
        }

        private IWatcher GetWatcher ()
        {
            return WebWatcher.Create(_setting.Identifier, _setting.Url, Request, config =>
            {
                config.WithHttpServiceProvider(() => ServiceClient);
                config.EnsureThat(response => response.StatusCode == HttpStatusCode.OK);
            });
        }

        IHttpRequest Request
        {
            get
            {
                switch (_setting.HttpMethod)
                {
                    case "GET":
                        return HttpRequest.Get(_setting.HeaderProperties);
                    case "POST":
                        return HttpRequest.Post(_setting.Data, _setting.HeaderProperties);
                    case "PUT":
                        return HttpRequest.Put(_setting.Data, _setting.HeaderProperties);
                    case "DELETE":
                        return HttpRequest.Delete(_setting.HeaderProperties);
                    default:
                        return HttpRequest.Get(_setting.HeaderProperties);
                }
            }
        }

        IHttpService ServiceClient
        {
            get
            {
                HttpClient client = GetClient();
                Decorate(client);
                return new HttpService(client);
            }
        }

        private HttpClient GetClient ()
        {
            var auth = GetAuthAttribute().Result;
            if (auth == null || auth.GetType() == typeof(string))
            {
                return CreateClientInternal(false, null);
            }
            else
            {
                var handler = GetWebHttpHandler((X509Certificate2)auth);

                if (handler == null)
                {
                    return CreateClientInternal(false, null);
                }

                return CreateClientInternal(true, handler);
            }
        }

        private HttpMessageHandler GetWebHttpHandler (X509Certificate2 cert)
        {
            HttpClientHandler clientHandler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual
            };
            clientHandler.ClientCertificates.Add(cert);
            return clientHandler;
        }

        private HttpClient CreateClientInternal (bool needsHttpHandler, HttpMessageHandler handler)
        {
            if (needsHttpHandler)
            {
                return new HttpClient(handler);
            }
            return new HttpClient();
        }

        void Decorate (HttpClient client)
        {
            CustomizeClient(client);

            if (_setting.HeaderProperties == null || !_setting.HeaderProperties.Any())
            {
                return;
            }

            foreach (var c in _setting.HeaderProperties)
            {
                if (client.DefaultRequestHeaders.Contains(c.Key))
                {
                    continue;
                }
                client.DefaultRequestHeaders.Add(c.Key, c.Value);
            }
        }

        void CustomizeClient (HttpClient client)
        {
            client.Timeout = _setting.ConnectionTimeout;

            if (!client.DefaultRequestHeaders.Contains("ConnectionClose"))
            {
                client.DefaultRequestHeaders.ConnectionClose = true;
            }
        }

        private async Task<object> GetAuthAttribute ()
        {
            if (_setting.AuthenticationCallback == null)
            {
                return null;
            }

            var auth = await _setting.AuthenticationCallback();
            if (auth == null)
            {
                return null;
            }

            if (auth is string)
            {
                return (string)auth;
            }

            else if (auth is X509Certificate2)
            {
                return auth;
            }

            return null;
        }

        public override Task Initialize ()
        {
            if (_setting == null)
            {
                return Task.CompletedTask;
            }

            ConfigureWatchDog();
            return Task.CompletedTask;
        }

        private void ConfigureWatchDog ()
        {
            if (_watchDog == null)
            {
                _watchDog = new WardenWatchDog();
            }

            _watchDog.AddWatcher(Watcher);
            _watchDog.OnSuccess(c => OnCompletion(c));
            _watchDog.OnFailure(c => OnCompletion(c));
            _watchDog.OnError(e => HandleError(e));
        }

        private void HandleError (Exception e)
        {
            _health.Add(new DependentComponentHealth
            {
                ComponentType = DependentComponentTypes.SqlServer,
                IsValid = false,
                Connection = _setting.Url,
                Identifier = _setting.Identifier,
                ErrorDetails = e.ToString()
            });
        }

        private void OnCompletion (IWardenCheckResult c)
        {
            _health.Add(new DependentComponentHealth
            {
                ComponentType = DependentComponentTypes.SqlServer,
                IsValid = c.IsValid,
                Connection = _setting.Url,
                Identifier = _setting.Identifier,
                ErrorDetails = c.Exception?.ToString(),
                Description = c.WatcherCheckResult?.Description,
                ExecutionTime = c.ExecutionTime,
                StartedAt = c.StartedAt,
                CompletedAt = c.CompletedAt
            });
        }
    }
}
