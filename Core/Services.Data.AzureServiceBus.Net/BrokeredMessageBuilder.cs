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
using Microsoft.ServiceBus.Messaging;
using Services.Data.Common.Extensions;

namespace Services.Data.AzureServiceBus
{
    sealed class TopicBrokeredMessageBuilder<T>
    {
        readonly T _data;
        readonly IAzureServiceBusMessageWriterSetting _setting;

        internal TopicBrokeredMessageBuilder (T data, IAzureServiceBusMessageWriterSetting setting)
        {
            _data = data;
            _setting = setting;
        }

        internal BrokeredMessage Message
        {
            get
            {
                return BuildMessage();
            }
        }

        private BrokeredMessage BuildMessage ()
        {
            if (_setting == null)
            {
                return new BrokeredMessage();
            }

            return BuildMessageWithSettings();
        }

        private BrokeredMessage BuildMessageWithSettings ()
        {
            BrokeredMessage message;

            if (_data is string)
            {
                message = new BrokeredMessage((_data as string).Compress());
            }
            else
            {
                message = new BrokeredMessage(_data);
            }           

            if (message == default(BrokeredMessage))
            {
                throw LocalErrors.CouldNotCreateTopicClientWithSettings();
            }

            new TopicBrokeredMessageDecorator(message).Decorate(_setting);
            return message;
        }
    }
}
