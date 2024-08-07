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
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Services.Core.Common
{
    public static class ObjectExtensions
    {
        /// <summary>
        /// Deep clone
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        /// <returns></returns>
        public static T Clone<T> (this T message)
        {
            if (message == null)
            {
                return default(T);
            }

            IFormatter formatter = new BinaryFormatter();
            using (Stream stream = new MemoryStream())
            {
                formatter.Serialize(stream, message);
                stream.Seek(0, SeekOrigin.Begin);
// CodeQL [SM04191] No security risk associated with this piece of code.
                return (T)formatter.Deserialize(stream);
            }
        }

        /// <summary>
        /// Converts bytes into a generic type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static T As<T> (this byte[] bytes)
        {
            if (bytes == null || !bytes.Any())
            {
                return default(T);
            }

            int offset = 0;
            var type = typeof(T);

            if (type == typeof(sbyte)) return (T)(object)((sbyte)bytes[offset]);
            else if (type == typeof(byte)) return (T)(object)bytes[offset];
            else if (type == typeof(short)) return (T)(object)BitConverter.ToInt16(bytes, offset);
            else if (type == typeof(ushort)) return (T)(object)BitConverter.ToUInt16(bytes, offset);
            else if (type == typeof(int)) return (T)(object)BitConverter.ToInt32(bytes, offset);
            else if (type == typeof(uint)) return (T)(object)BitConverter.ToUInt32(bytes, offset);
            else if (type == typeof(long)) return (T)(object)BitConverter.ToInt64(bytes, offset);
            else if (type == typeof(ulong)) return (T)(object)BitConverter.ToUInt64(bytes, offset);
            else if (type == typeof(string)) return (T)(object)Encoding.Default.GetString(bytes).Trim('"');
            else if (type == typeof(double)) return (T)(object)BitConverter.ToDouble(bytes, offset);
            else if (type == typeof(float)) return (T)(object)BitConverter.ToSingle(bytes, offset);
            else if (type == typeof(bool)) return (T)(object)BitConverter.ToBoolean(bytes, offset);

            else throw new NotSupportedException($"Type conversion is not supported for this {type}");
        }
    }
}
