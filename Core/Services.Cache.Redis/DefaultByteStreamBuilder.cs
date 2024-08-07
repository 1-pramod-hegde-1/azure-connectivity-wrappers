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
using System.Xml;
using System.Xml.Serialization;

namespace Services.Cache.Redis
{
    class DefaultByteStreamBuilder : ICacheItemBuilder
    {
        dynamic ICacheItemBuilder.BuildCacheItem (object value)
        {
            if (value is byte[])
            {
                return value as byte[];
            }

            byte[] inb = GetSerializedValue(value);
            return GetCompressedValue(inb);
        }

        private byte[] GetCompressedValue (byte[] inbytes)
        {
            byte[] outbytes;
            using (MemoryStream ostream = new MemoryStream())
            {
                using (DeflateStream df = new DeflateStream(ostream, CompressionMode.Compress, true))
                {
                    df.Write(inbytes, 0, inbytes.Length);
                }
                outbytes = ostream.ToArray();
            }

            return outbytes;
        }

        private byte[] GetSerializedValue (object value)
        {
            if (!value.GetType().IsSerializable)
            {
                throw new ArgumentException("Object is not serializable");
            }

            using (MemoryStream stream = new MemoryStream())
            {
                DoXmlSerialize(value, stream);
                return stream.ToArray();
            }
        }

        private void DoXmlSerialize (object value, MemoryStream stream)
        {
            using (XmlWriter wtr = XmlWriter.Create(stream))
            {
                XmlSerializer serializer = new XmlSerializer(value.GetType());
                serializer.Serialize(wtr, value);
                stream.Flush();
            }
        }
    }
}
