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
using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace Services.Data.AzureServiceBus
{
    public static class ServiceBusMessageExtensions
    {
        public static T GetBody<T>(this ServiceBusMessage message)
        {
            if (message == null || message.Body == null)
            {
                return default(T);
            }

            return GetInternalBody<T>(message.Body.ToArray());
        }

        public static T GetBody<T>(this ServiceBusReceivedMessage message)
        {
            if (message == null || message.Body == null)
            {
                return default(T);
            }

            string messageBody = message.Body.ToString();

            if (messageBody.StartsWith("@") && messageBody.Contains(@"http://schemas.microsoft.com/2003/10/Serialization"))
            {
                var deserializer = new DataContractSerializer(typeof(string));
                XmlDictionaryReader reader = XmlDictionaryReader.CreateBinaryReader(message.Body.ToStream(), XmlDictionaryReaderQuotas.Max);
                
                messageBody = (string)deserializer.ReadObject(reader);
            }

            return GetInternalBody<T>(messageBody);
        }

        private static T GetInternalBody<T>(string body)
        {
            if (typeof(T) == typeof(string))
            {
                return (T)Convert.ChangeType(body, typeof(T));
            }

            object _t = JsonConvert.DeserializeObject(body);

            if (_t.GetType() != typeof(T))
            {
                throw LocalErrors.CannotCastBodyToGeneric<T>();
            }

            return (T)Convert.ChangeType(_t, typeof(T));
        }

        private static T GetInternalBody<T>(byte[] body)
        {
            if (typeof(T) == typeof(string))
            {
                return (T)Convert.ChangeType(Encoding.UTF8.GetString(body), typeof(T));
            }

            object _t = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(body));

            if (_t.GetType() != typeof(T))
            {
                throw LocalErrors.CannotCastBodyToGeneric<T>();
            }

            return (T)Convert.ChangeType(_t, typeof(T));
        }
    }
}
