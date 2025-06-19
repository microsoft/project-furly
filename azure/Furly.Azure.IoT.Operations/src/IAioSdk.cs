// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Operations.Services
{
    using global::Azure.Iot.Operations.Connector;
    using global::Azure.Iot.Operations.Protocol;
    using global::Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
    using global::Azure.Iot.Operations.Services.StateStore;
    using global::Azure.Iot.Operations.Services.SchemaRegistry;

    /// <summary>
    /// Aio sdk
    /// </summary>
    public interface IAioSdk
    {
        /// <summary>
        /// Create adr service client
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        IAdrServiceClient CreateAdrServiceClient(IMqttPubSubClient client);

        /// <summary>
        /// Create adr client wrapper
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        IAdrClientWrapper CreateAdrClientWrapper(IMqttPubSubClient client);

        /// <summary>
        /// Create state store client
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        IStateStoreClient CreateStateStoreClient(IMqttPubSubClient client);

        /// <summary>
        /// Create schema registry client
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        ISchemaRegistryClient CreateSchemaRegistryClient(IMqttPubSubClient client);
    }
}
