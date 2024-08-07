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
using System.Threading.Tasks;

namespace Services.Communication.Http
{
    public delegate Task<object> AuthenticationCallback();

    public interface ICommunicationClient : ICompositionPart
    {
        string this[string key] { get; }
        Task<T> GetAsync<T>(string resourceUri);
        Task PostAsync(string resourceUri, object content);
        Task<T> PostAsync<T>(string resourceUri, object content);
        Task PatchAsync(string resourceUri, object content);
        Task<T> PatchAsync<T>(string resourceUri, object content);
        Task DeleteAsync(string resourceUri, object content);
        Task<T> DeleteAsync<T>(string resourceUri, object content);
        Task PutAsync(string resourceUri, object content);
        Task<T> PutAsync<T>(string resourceUri, object content);
        void AddHeader(string name, string value);
		void RemoveHeader(string name);
		void ClearHeader();
		void ConfigureClient<TCache>(string baseUri, AuthenticationCallback authCallback = null, TCache cache = default, TimeSpan timeout = default);
    }
}
