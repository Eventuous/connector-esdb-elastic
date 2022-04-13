using Eventuous.Connector.EsdbElastic.Conversions;
using Eventuous.ElasticSearch.Producers;
using Eventuous.ElasticSearch.Store;
using Eventuous.Gateway;
using Eventuous.Subscriptions.Context;

namespace connector_esdb_elastic;

public class EventTransform : IGatewayTransform<ElasticProduceOptions> {
    readonly string _indexName;

    static readonly ElasticProduceOptions Options = new() { ProduceMode = ProduceMode.Create };

    public EventTransform(string indexName) => _indexName = indexName;

    public ValueTask<GatewayContext<ElasticProduceOptions>?> RouteAndTransform(IMessageConsumeContext context) {
        var ctx = new GatewayContext<ElasticProduceOptions>(
            new StreamName(_indexName),
            FromContext(context),
            null,
            Options
        );

        return new ValueTask<GatewayContext<ElasticProduceOptions>?>(ctx);
    }
    
    static PersistedEvent FromContext(IMessageConsumeContext ctx)
        => new(
            ctx.MessageId,
            ctx.MessageType,
            ctx.StreamPosition,
            ctx.ContentType,
            ctx.Stream,
            ctx.GlobalPosition,
            ctx.Message,
            ElasticMetadata.FromMetadata(ctx.Metadata),
            ctx.Created
        );
}
