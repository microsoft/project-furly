// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Dapr.Clients
{
    using Furly.Extensions.Dapr.Tests.Grpc.v1;
    using Furly.Extensions.Messaging;
    using Google.Protobuf;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Connector to sidecar
    /// </summary>
    public sealed class DaprSidecarConnector : IEventSubscriber,
        IDaprSidecarStorage, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "Dapr";

        /// <inheritdoc/>
        public int Port { get; internal set; }

        /// <inheritdoc/>
        public bool HasNoQuerySupport { get; set; }

        /// <inheritdoc/>
        public ConcurrentDictionary<string, ByteString> Items { get; } = new();

        /// <inheritdoc/>
        public async Task WaitUntil(string key, bool available)
        {
            // TODO: Make nicer
            for (var i = 0; i < 10; i++)
            {
                if (!(available ^ Items.ContainsKey(key)))
                {
                    return;
                }
                await Task.Delay(100).ConfigureAwait(false);
            }
        }

        public DaprSidecarConnector(int port)
        {
            Port = port;
            _cts = new CancellationTokenSource();
            _running = RunAsync(_cts.Token);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                _cts.Cancel();
                _running.GetAwaiter().GetResult();
            }
            finally
            {
                _cts.Dispose();
            }
        }

        /// <inheritdoc/>
        public ValueTask<IAsyncDisposable> SubscribeAsync(string topic,
            IEventConsumer consumer, CancellationToken ct = default)
        {
            var subscription = new Subscription(topic, consumer, this);
            lock (_subscriptions)
            {
                _subscriptions.Add(subscription);
            }
            return ValueTask.FromResult<IAsyncDisposable>(subscription);
        }

        /// <summary>
        /// Signal publishing
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task OnPublishEventReceivedAsync(PublishEventRequest request)
        {
            IEnumerable<Task> handles;
            lock (_subscriptions)
            {
                handles = _subscriptions
                    .Where(subscription => subscription.Matches(request.Topic))
                    .Select(subscription => subscription.Consumer.HandleAsync(
                        request.Topic, new ReadOnlySequence<byte>(request.Data.Memory),
                        request.DataContentType, request.Metadata, null));
            }
            await Task.WhenAll(handles).ConfigureAwait(false);
        }

        /// <summary>
        /// Run the grpc service
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task RunAsync(CancellationToken token)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options =>
                options.ListenAnyIP(Port,
                o => o.Protocols = HttpProtocols.Http2));
            builder.Services.AddGrpc();
            builder.Services.AddSingleton(GetType(), this);

            var app = builder.Build();
            await using (app.ConfigureAwait(false))
            {
                // Configure the HTTP request pipeline.
                app.MapGrpcService<DaprSidecar>();

                await app.StartAsync(token).ConfigureAwait(false);
                await app.WaitForShutdownAsync(token).ConfigureAwait(false);
            }
        }

        private sealed class Subscription : IAsyncDisposable
        {
            /// <summary>
            /// Consumer
            /// </summary>
            public IEventConsumer Consumer { get; }

            /// <summary>
            /// Registered target
            /// </summary>
            public string Filter { get; }

            public Subscription(string topicFilter, IEventConsumer consumer,
                DaprSidecarConnector outer)
            {
                _outer = outer;
                Consumer = consumer;
                Filter = topicFilter;
            }

            /// <summary>
            /// Returns true if the target matches the subscription.
            /// </summary>
            /// <param name="topic"></param>
            /// <returns></returns>
            public bool Matches(string topic)
            {
                return TopicFilter.Matches(topic, Filter);
            }

            /// <inheritdoc/>
            public ValueTask DisposeAsync()
            {
                lock (_outer._subscriptions)
                {
                    _outer._subscriptions.Remove(this);
                }
                return ValueTask.CompletedTask;
            }

            private readonly DaprSidecarConnector _outer;
        }

        private readonly List<Subscription> _subscriptions = [];
        private readonly CancellationTokenSource _cts;
        private readonly Task _running;
    }
}
