// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Dapr.Clients
{
    using Furly.Extensions.Dapr.Tests.Grpc.v1;
    using Google.Protobuf.WellKnownTypes;
    using Grpc.Core;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Side car service
    /// </summary>
    public sealed class DaprSidecar : Dapr.DaprBase
    {
        private readonly DaprSidecarConnector _connector;

        /// <summary>
        /// Create side car
        /// </summary>
        public DaprSidecar(DaprSidecarConnector connector)
        {
            _connector = connector;
        }

        /// <inheritdoc/>
        public override async Task<Empty> PublishEvent(PublishEventRequest request,
            ServerCallContext context)
        {
            await _connector.OnPublishEventReceivedAsync(request).ConfigureAwait(false);
            return new Empty();
        }

        /// <inheritdoc/>
        public override Task<BulkPublishResponse> BulkPublishEventAlpha1(
            BulkPublishRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<Empty> DeleteState(DeleteStateRequest request,
            ServerCallContext context)
        {
            _connector.Items.TryRemove(request.Key, out _);
            return Task.FromResult(new Empty());
        }

        /// <inheritdoc/>
        public override Task<GetStateResponse> GetState(GetStateRequest request,
            ServerCallContext context)
        {
            var response = new GetStateResponse();
            if (_connector.Items.TryGetValue(request.Key, out var value))
            {
                response.Data = value;
            }
            return Task.FromResult(response);
        }

        /// <inheritdoc/>
        public override Task<Empty> SaveState(SaveStateRequest request,
            ServerCallContext context)
        {
            foreach (var state in request.States)
            {
                _connector.Items.AddOrUpdate(state.Key, _ => state.Value, (_, _) => state.Value);
            }
            return Task.FromResult(new Empty());
        }

        /// <inheritdoc/>
        public override Task<QueryStateResponse> QueryStateAlpha1(
            QueryStateRequest request, ServerCallContext context)
        {
            var response = new QueryStateResponse();
            if (request.Query == "{}" && !_connector.HasNoQuerySupport)
            {
                response.Results.Add(_connector.Items.Select(s => new QueryStateItem
                {
                    Data = s.Value,
                    Key = s.Key,
                }));
            }
            return Task.FromResult(response);
        }

        /// <inheritdoc/>
        public override Task<Empty> SetMetadata(SetMetadataRequest request,
            ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<GetMetadataResponse> GetMetadata(Empty request,
            ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<Empty> DeleteBulkState(
            DeleteBulkStateRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<Empty> ExecuteStateTransaction(
            ExecuteStateTransactionRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<GetBulkSecretResponse> GetBulkSecret(
            GetBulkSecretRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<GetBulkStateResponse> GetBulkState(
            GetBulkStateRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<GetConfigurationResponse> GetConfiguration(
            GetConfigurationRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<GetSecretResponse> GetSecret(GetSecretRequest request,
            ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<InvokeResponse> InvokeService(
            InvokeServiceRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<Empty> PauseWorkflowAlpha1(PauseWorkflowRequest request,
            ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<Empty> PurgeWorkflowAlpha1(PurgeWorkflowRequest request,
            ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<Empty> RaiseEventWorkflowAlpha1(
            RaiseEventWorkflowRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<Empty> ResumeWorkflowAlpha1(ResumeWorkflowRequest request,
            ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<StartWorkflowResponse> StartWorkflowAlpha1(
            StartWorkflowRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task SubscribeConfiguration(SubscribeConfigurationRequest request,
            IServerStreamWriter<SubscribeConfigurationResponse> responseStream,
            ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<TryLockResponse> TryLockAlpha1(TryLockRequest request,
            ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<UnlockResponse> UnlockAlpha1(UnlockRequest request,
            ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<UnsubscribeConfigurationResponse> UnsubscribeConfiguration(
            UnsubscribeConfigurationRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task DecryptAlpha1(IAsyncStreamReader<DecryptRequest> requestStream,
            IServerStreamWriter<DecryptResponse> responseStream, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task EncryptAlpha1(IAsyncStreamReader<EncryptRequest> requestStream,
            IServerStreamWriter<EncryptResponse> responseStream, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<SubtleDecryptResponse> SubtleDecryptAlpha1(
            SubtleDecryptRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<SubtleEncryptResponse> SubtleEncryptAlpha1(
            SubtleEncryptRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<SubtleGetKeyResponse> SubtleGetKeyAlpha1(
            SubtleGetKeyRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<SubtleSignResponse> SubtleSignAlpha1(
            SubtleSignRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<SubtleUnwrapKeyResponse> SubtleUnwrapKeyAlpha1(
            SubtleUnwrapKeyRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<SubtleVerifyResponse> SubtleVerifyAlpha1(
            SubtleVerifyRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<SubtleWrapKeyResponse> SubtleWrapKeyAlpha1(
            SubtleWrapKeyRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<Empty> Shutdown(Empty request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }
    }
}
