// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using Furly.Extensions.Mqtt;
    using global::Azure.Iot.Operations.Protocol.Events;
    using global::Azure.Iot.Operations.Protocol.Models;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Converts from and to sdk types
    /// </summary>
    internal static class Extensions
    {
        /// <summary>
        /// Convert topic filter
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static MqttTopicFilter ToSdkType(this MQTTnet.Packets.MqttTopicFilter filter)
        {
            return new MqttTopicFilter(filter.Topic)
            {
                QualityOfServiceLevel = (MqttQualityOfServiceLevel)
                    (int)filter.QualityOfServiceLevel,
                NoLocal = filter.NoLocal,
                RetainHandling = (MqttRetainHandling)(int)filter.RetainHandling,
                RetainAsPublished = filter.RetainAsPublished,
            };
        }

        /// <summary>
        /// Convert topic filter
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static MQTTnet.Packets.MqttTopicFilter FromSdkType(this MqttTopicFilter filter)
        {
            var builder = new MQTTnet.MqttTopicFilterBuilder()
                .WithTopic(filter.Topic)
                .WithRetainHandling((MQTTnet.Protocol.MqttRetainHandling)
                    (int)filter.RetainHandling)
                .WithNoLocal(filter.NoLocal)
                .WithRetainAsPublished(filter.RetainAsPublished);

            switch (filter.QualityOfServiceLevel)
            {
                case MqttQualityOfServiceLevel.AtMostOnce:
                    builder.WithAtMostOnceQoS();
                    break;
                case MqttQualityOfServiceLevel.AtLeastOnce:
                    builder.WithAtLeastOnceQoS();
                    break;
                default:
                    builder.WithExactlyOnceQoS();
                    break;
            }
            return builder.Build();
        }

        /// <summary>
        /// Convert message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static MqttApplicationMessage ToSdkType(this MQTTnet.MqttApplicationMessage message)
        {
            return new MqttApplicationMessage(message.Topic)
            {
                QualityOfServiceLevel =
                    (MqttQualityOfServiceLevel)(int)message.QualityOfServiceLevel,
                ContentType = message.ContentType,
                CorrelationData = message.CorrelationData,
                MessageExpiryInterval = message.MessageExpiryInterval,
                PayloadSegment = message.Payload.ToArray(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)message.PayloadFormatIndicator,
                Retain = message.Retain,
                SubscriptionIdentifiers = message.SubscriptionIdentifiers,
                TopicAlias = message.TopicAlias,
                ResponseTopic = message.ResponseTopic,
                Dup = message.Dup,
                UserProperties = ToSdkType(message.UserProperties)
            };
        }

        /// <summary>
        /// Convert message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static MQTTnet.MqttApplicationMessage FromSdkType(this MqttApplicationMessage message)
        {
            var mqttNetMessageBuilder = new MQTTnet.MqttApplicationMessageBuilder()
                .WithTopicAlias(message.TopicAlias)
                .WithTopic(message.Topic)
                .WithContentType(message.ContentType)
                .WithCorrelationData(message.CorrelationData)
                .WithMessageExpiryInterval(message.MessageExpiryInterval)
                .WithPayload(message.Payload)
                .WithPayloadFormatIndicator(message.PayloadFormatIndicator
                    == MqttPayloadFormatIndicator.Unspecified ?
                        MQTTnet.Protocol.MqttPayloadFormatIndicator.Unspecified :
                        MQTTnet.Protocol.MqttPayloadFormatIndicator.CharacterData)
                .WithQualityOfServiceLevel(message.QualityOfServiceLevel
                    == MqttQualityOfServiceLevel.AtMostOnce ?
                        MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce :
                        message.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtLeastOnce ?
                        MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce :
                        MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                .WithResponseTopic(message.ResponseTopic)
                .WithRetainFlag(message.Retain);

            if (message.SubscriptionIdentifiers != null)
            {
                foreach (var subscriptionIdentifier in message.SubscriptionIdentifiers)
                {
                    mqttNetMessageBuilder.WithSubscriptionIdentifier(subscriptionIdentifier);
                }
            }
            if (message.UserProperties != null)
            {
                foreach (var userProperty in message.UserProperties)
                {
                    mqttNetMessageBuilder.WithUserProperty(userProperty.Name, userProperty.Value);
                }
            }
            return mqttNetMessageBuilder.Build();
        }

        /// <summary>
        /// Convert user properties
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        public static List<MqttUserProperty> ToSdkType(
            this IReadOnlyCollection<MQTTnet.Packets.MqttUserProperty>? properties)
        {
            var genericUserProperties = new List<MqttUserProperty>();
            if (properties != null)
            {
                foreach (var mqttNetUserProperty in properties)
                {
                    genericUserProperties.Add(new MqttUserProperty(
                        mqttNetUserProperty.Name, mqttNetUserProperty.Value));
                }
            }
            return genericUserProperties;
        }

        /// <summary>
        /// Convert properties
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        public static List<MQTTnet.Packets.MqttUserProperty>? FromSdkType(
            this List<MqttUserProperty>? properties)
        {
            if (properties == null)
            {
                return null;
            }
            var mqttNetUserProperties = new List<MQTTnet.Packets.MqttUserProperty>();
            foreach (var mqttNetUserProperty in properties)
            {
                mqttNetUserProperties.Add(new MQTTnet.Packets.MqttUserProperty(
                    mqttNetUserProperty.Name, mqttNetUserProperty.Value));
            }
            return mqttNetUserProperties;
        }

        /// <summary>
        /// Convert options
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static MQTTnet.MqttClientUnsubscribeOptions FromSdkType(
            this MqttClientUnsubscribeOptions options)
        {
            var mqttNetOptions = new MQTTnet.MqttClientUnsubscribeOptions
            {
                UserProperties = options.UserProperties.FromSdkType()
            };
            mqttNetOptions.TopicFilters.AddRange(options.TopicFilters);
            return mqttNetOptions;
        }

        /// <summary>
        /// Convert options
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static MQTTnet.MqttClientSubscribeOptions FromSdkType(
            this MqttClientSubscribeOptions options)
        {
            var mqttNetOptions = new MQTTnet.MqttClientSubscribeOptions
            {
                UserProperties = options.UserProperties.FromSdkType(),
                SubscriptionIdentifier = options.SubscriptionIdentifier
            };
            foreach (var topicFilter in options.TopicFilters)
            {
                mqttNetOptions.TopicFilters.Add(new()
                {
                    RetainAsPublished = topicFilter.RetainAsPublished,
                    NoLocal = topicFilter.NoLocal,
                    QualityOfServiceLevel =
                        (MQTTnet.Protocol.MqttQualityOfServiceLevel)
                            (int)topicFilter.QualityOfServiceLevel,
                    RetainHandling = (MQTTnet.Protocol.MqttRetainHandling)
                        (int)topicFilter.RetainHandling,
                    Topic = topicFilter.Topic,
                });
            }
            return mqttNetOptions;
        }

        /// <summary>
        /// Convert result
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public static MqttClientSubscribeResult ToSdkType(this MQTTnet.MqttClientSubscribeResult result)
        {
            List<MqttClientSubscribeResultItem> genericItems = [];
            foreach (var mqttNetItem in result.Items)
            {
                genericItems.Add(new MqttClientSubscribeResultItem(
                    ToSdkType(mqttNetItem.TopicFilter),
                    (MqttClientSubscribeReasonCode)(int)mqttNetItem.ResultCode));
            }
            return new MqttClientSubscribeResult(result.PacketIdentifier, genericItems,
                result.ReasonString, ToSdkType(result.UserProperties));
        }

        /// <summary>
        /// Convert result
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public static MqttClientUnsubscribeResult ToSdkType(this MQTTnet.MqttClientUnsubscribeResult result)
        {
            List<MqttClientUnsubscribeResultItem> genericItems = [];
            foreach (var mqttNetItem in result.Items)
            {
                genericItems.Add(new MqttClientUnsubscribeResultItem(
                    mqttNetItem.TopicFilter,
                    (MqttClientUnsubscribeReasonCode)(int)mqttNetItem.ResultCode));
            }

            return new MqttClientUnsubscribeResult(result.PacketIdentifier,
                genericItems, result.ReasonString, ToSdkType(result.UserProperties));
        }

        /// <summary>
        /// Convert result
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public static MqttClientPublishResult ToSdkType(this MQTTnet.MqttClientPublishResult result)
        {
            return new MqttClientPublishResult(result.PacketIdentifier,
                (MqttClientPublishReasonCode)(int)result.ReasonCode, result.ReasonString,
                ToSdkType(result.UserProperties));
        }

        /// <summary>
        /// Convert event arg
        /// </summary>
        /// <param name="args"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        public static MqttApplicationMessageReceivedEventArgs ToSdkType(
            this MqttMessageReceivedEventArgs args,
            Func<MqttApplicationMessageReceivedEventArgs, CancellationToken, Task> handler)
        {
            return new MqttApplicationMessageReceivedEventArgs(args.ClientId,
                ToSdkType(args.ApplicationMessage), args.PacketIdentifier, handler);
        }
    }
}
