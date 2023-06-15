// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Connector.EsdbGenericGrpc;

public class PassThroughSerializer : IEventSerializer {
    public DeserializationResult DeserializeEvent(ReadOnlySpan<byte> data, string eventType, string contentType)
        => new SuccessfullyDeserialized(data.ToArray());

    public SerializationResult SerializeEvent(object evt) => new("", "application/json", (byte[])evt);
}
