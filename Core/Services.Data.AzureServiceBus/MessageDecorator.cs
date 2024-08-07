﻿// Copyright (c) [2022] Pramod Hegde
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
using System.Collections.Generic;
using System.Linq;
using Azure.Messaging.ServiceBus;

namespace Services.Data.AzureServiceBus
{
    sealed class MessageDecorator
    {
        private readonly ServiceBusMessage message;

        internal MessageDecorator (ServiceBusMessage message)
        {
            this.message = message;
        }

        internal void Decorate (IAzureServiceBusMessageWriterSetting setting)
        {
            message.ContentType = setting.ContentType;
            message.CorrelationId = string.IsNullOrWhiteSpace(setting.CorrelationId) ? setting.CorrelationId : Guid.NewGuid().ToString();
            message.PartitionKey = setting.PartitionKey;
            message.MessageId = setting.MessageId;
            message.SessionId = setting.SessionId;

            if (setting.MessageProperties != null && setting.MessageProperties.Any())
            {
                AddMessageProperties(message, setting.MessageProperties);
            }

            if (setting.EnableDelayedMessaging) 
            {
                message.ScheduledEnqueueTime = setting.ScheduledEnqueueTime;
            }
        }

        private void AddMessageProperties (ServiceBusMessage message, IDictionary<string, object> messageProperties)
        {
            foreach (var p in messageProperties)
            {
                message.ApplicationProperties.Add(p);
            }
        }
    }
}