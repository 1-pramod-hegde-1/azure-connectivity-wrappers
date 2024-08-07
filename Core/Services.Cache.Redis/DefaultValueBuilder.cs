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
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Services.Cache.Redis
{
    sealed class DefaultValueBuilder : ICacheItemBuilder
    {
        dynamic ICacheItemBuilder.BuildCacheItem (object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is string)
            {
                return value as string;
            }

            if (value is byte[])
            {
                return Encoding.Default.GetString(value as byte[]);
            }

            if (value.GetType().IsValueType)
            {
                return Convert.ToString(value);
            }

            return GetRedisValueForComplexType(value);
        }

        private dynamic GetRedisValueForComplexType (object value)
        {
            byte[] inBytes = Serialize(value);
            using (var ms = new MemoryStream())
            {
                using (DeflateStream df = new DeflateStream(ms, CompressionMode.Compress, true))
                {
                    df.Write(inBytes, 0, inBytes.Length);
                }
                return ms.ToArray();
            }
        }

        private byte[] Serialize (object value)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                DataContractJsonSerializer ser = new DataContractJsonSerializer(value.GetType(), GetKnownTypes());
                ser.WriteObject(ms, value);
                return ms.ToArray();
            }
        }

        private Type[] GetKnownTypes ()
        {
            return new[] { typeof(DBNull) };
        }
    }
}
