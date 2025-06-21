namespace Furly.Azure.IoT.Operations.Mock
{
    using global::Azure.IoT.Operations.Mock.AdrBaseService;
    using global::Azure.Iot.Operations.Protocol.RPC;
    using global::Azure.Iot.Operations.Protocol;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Create service
    /// </summary>
#pragma warning disable CA1515 // Consider making public types internal
    public sealed class AdrService : AdrBaseService.Service
#pragma warning restore CA1515 // Consider making public types internal
    {
        public AdrService(ApplicationContext applicationContext, IMqttPubSubClient mqttClient,
            Dictionary<string, string>? topicTokenMap = null)
            : base(applicationContext, mqttClient, topicTokenMap)
        {
        }

        public override Task<ExtendedResponse<CreateOrUpdateDiscoveredAssetResponsePayload>> CreateOrUpdateDiscoveredAssetAsync(
            CreateOrUpdateDiscoveredAssetRequestPayload request, CommandRequestMetadata requestMetadata,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<ExtendedResponse<GetAssetResponsePayload>> GetAssetAsync(
            GetAssetRequestPayload request, CommandRequestMetadata requestMetadata,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<ExtendedResponse<GetAssetStatusResponsePayload>> GetAssetStatusAsync(
            GetAssetStatusRequestPayload request, CommandRequestMetadata requestMetadata,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<ExtendedResponse<GetDeviceResponsePayload>> GetDeviceAsync(
            CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<ExtendedResponse<GetDeviceStatusResponsePayload>> GetDeviceStatusAsync(
            CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<ExtendedResponse<SetNotificationPreferenceForAssetUpdatesResponsePayload>> SetNotificationPreferenceForAssetUpdatesAsync(
            SetNotificationPreferenceForAssetUpdatesRequestPayload request, CommandRequestMetadata requestMetadata,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<ExtendedResponse<SetNotificationPreferenceForDeviceUpdatesResponsePayload>> SetNotificationPreferenceForDeviceUpdatesAsync(
            SetNotificationPreferenceForDeviceUpdatesRequestPayload request, CommandRequestMetadata requestMetadata,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<ExtendedResponse<UpdateAssetStatusResponsePayload>> UpdateAssetStatusAsync(
            UpdateAssetStatusRequestPayload request, CommandRequestMetadata requestMetadata,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<ExtendedResponse<UpdateDeviceStatusResponsePayload>> UpdateDeviceStatusAsync(
            UpdateDeviceStatusRequestPayload request, CommandRequestMetadata requestMetadata,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
