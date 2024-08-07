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
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;

namespace Services.Communication.Http
{
    /// <summary>
    /// Abstracts all successful http response codes other than 200, 204 and 206
    /// </summary>
    public class HttpStatusCodeException : HttpListenerException
    {
        public HttpResponseMessage ResponseMessage { get; set; }

        public HttpStatusCodeException()
        {
        }

        public HttpStatusCodeException(int errorCode) : base(errorCode)
        {
        }

        public HttpStatusCodeException(string message, HttpResponseMessage responseMessage) : base((int)responseMessage.StatusCode, $"{message}. {responseMessage.ReasonPhrase}")
        {
            ResponseMessage = responseMessage;
            this.Data.Add("Message", message);
            this.Data.Add("Response.StatusCode", responseMessage.StatusCode);
            this.Data.Add("Response.ReasonPhrase", responseMessage.ReasonPhrase);
            this.Data.Add("Response.Content", responseMessage.Content);
            this.Data.Add("Request.RequestUri", responseMessage.RequestMessage.RequestUri);
            this.Data.Add("Request.Headers", JsonConvert.SerializeObject(responseMessage.RequestMessage.Headers));
        }

        public HttpStatusCodeException(HttpResponseMessage responseMessage) : base((int)responseMessage.StatusCode, responseMessage.ReasonPhrase)
        {
            ResponseMessage = responseMessage;            
            this.Data.Add("Response.StatusCode", responseMessage.StatusCode);
            this.Data.Add("Response.ReasonPhrase", responseMessage.ReasonPhrase);
            this.Data.Add("Response.Content", responseMessage.Content);
            this.Data.Add("Request.RequestUri", responseMessage.RequestMessage.RequestUri);
            this.Data.Add("Request.Headers", JsonConvert.SerializeObject(responseMessage.RequestMessage.Headers));
        }

        public HttpStatusCodeException(int errorCode, string message) : base(errorCode, message)
        {
        }

        protected HttpStatusCodeException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext)
        {
        }
    }
}
