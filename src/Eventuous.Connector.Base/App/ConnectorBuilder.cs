// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Eventuous.Connector.Base.Config;
using Eventuous.Connector.Tools;
using Eventuous.Gateway;
using Eventuous.Producers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Polly;
using Eventuous.Subscriptions.Registrations;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable CheckNamespace

namespace Microsoft.Extensions.DependencyInjection;

public class ConnectorBuilder {
    [PublicAPI]
    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    public ConnectorBuilder<TSubscription, TSubscriptionOptions> SubscribeWith<TSubscription, TSubscriptionOptions>(string subscriptionId)
        where TSubscription : EventSubscription<TSubscriptionOptions> where TSubscriptionOptions : SubscriptionOptions
        => new(subscriptionId);
}

public class ConnectorBuilder<TSubscription, TSubscriptionOptions> : ConnectorBuilder
    where TSubscription : EventSubscription<TSubscriptionOptions> where TSubscriptionOptions : SubscriptionOptions {
    internal string SubscriptionId { get; }

    internal ConnectorBuilder(string subscriptionId) => SubscriptionId = subscriptionId;

    [PublicAPI]
    public ConnectorBuilder<TSubscription, TSubscriptionOptions> ConfigureSubscriptionOptions(Action<TSubscriptionOptions> configureOptions) {
        _configureOptions = configureOptions;
        return this;
    }

    [PublicAPI]
    public ConnectorBuilder<TSubscription, TSubscriptionOptions> ConfigureSubscription(Action<SubscriptionBuilder<TSubscription, TSubscriptionOptions>> configure) {
        _configure = configure;
        return this;
    }

    [PublicAPI]
    public ConnectorBuilder<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions>
        ProduceWith<TProducer, TProduceOptions>(ResolveRetryPolicy? retryPolicy = null, bool awaitProduce = true)
        where TProducer : class, IEventProducer<TProduceOptions> where TProduceOptions : class
        => new(this, retryPolicy, awaitProduce);

    internal void ConfigureOptions(TSubscriptionOptions options) => _configureOptions?.Invoke(options);

    internal void Configure(SubscriptionBuilder<TSubscription, TSubscriptionOptions> builder) => _configure?.Invoke(builder);

    Action<TSubscriptionOptions>?                                     _configureOptions;
    Action<SubscriptionBuilder<TSubscription, TSubscriptionOptions>>? _configure;
}

public class ConnectorBuilder<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions>
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TSubscriptionOptions : SubscriptionOptions
    where TProducer : class, IEventProducer<TProduceOptions>
    where TProduceOptions : class {
    readonly ConnectorBuilder<TSubscription, TSubscriptionOptions> _inner;
    readonly ResolveRetryPolicy?                                   _resolveRetryPolicy;
    Func<IServiceProvider, IGatewayTransform<TProduceOptions>>?    _getTransformer;
    readonly bool                                                  _awaitProduce;
    Type?                                                          _transformerType;

    public ConnectorBuilder(
        ConnectorBuilder<TSubscription, TSubscriptionOptions> inner,
        ResolveRetryPolicy?                                   resolveRetryPolicy,
        bool                                                  awaitProduce
    ) {
        _inner = inner;
        _resolveRetryPolicy = resolveRetryPolicy;
        _awaitProduce = awaitProduce;
    }

    [PublicAPI]
    public ConnectorBuilder<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions> TransformWith<T>(Func<IServiceProvider, T>? getTransformer)
        where T : class, IGatewayTransform<TProduceOptions> {
        _getTransformer = getTransformer;
        _transformerType = typeof(T);
        return this;
    }

    public void Register(IServiceCollection services) {
        services.AddSingleton(
            Ensure.NotNull(_transformerType, "Transformer"),
            Ensure.NotNull(_getTransformer, "GetTransformer")
        );

        services.TryAddSingleton<TProducer>();

        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            _inner.SubscriptionId,
            builder => {
                builder.Configure(_inner.ConfigureOptions);
                _inner.Configure(builder);
                builder.AddEventHandler(GetHandler);
            }
        );

        IEventHandler GetHandler(IServiceProvider sp) {
            var transform = sp.GetRequiredService(_transformerType!) as IGatewayTransform<TProduceOptions>;
            var producer  = sp.GetRequiredService<TProducer>();

            var handler = GatewayHandlerFactory.Create(producer, transform!.RouteAndTransform, _awaitProduce);
            return _resolveRetryPolicy == null ? handler : new PollyEventHandler(handler, _resolveRetryPolicy(sp));
        }
    }
}
