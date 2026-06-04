using MongoDB.Bson;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

internal static class SecEventDocumentBsonMapper
{
    public static BsonDocument ToBsonDocument(SecEventDocument doc)
    {
        var timestamp = doc.Timestamp.Kind == DateTimeKind.Utc ? doc.Timestamp : doc.Timestamp.ToUniversalTime();
        var ingestedAt = doc.IngestedAt.Kind == DateTimeKind.Utc ? doc.IngestedAt : doc.IngestedAt.ToUniversalTime();

        var bson = new BsonDocument
        {
            ["@timestamp"] = new BsonDateTime(timestamp),
            ["ingestedAt"] = new BsonDateTime(ingestedAt),
            ["domain"] = doc.Domain,
            ["source"] = new BsonDocument
            {
                ["type"] = ToBsonNullableString(doc.Source.Type),
                ["product"] = ToBsonNullableString(doc.Source.Product),
                ["host"] = ToBsonNullableString(doc.Source.Host)
            },
            ["event"] = new BsonDocument
            {
                ["action"] = doc.Event.Action,
                ["outcome"] = ToBsonNullableString(doc.Event.Outcome),
                ["code"] = ToBsonNullableString(doc.Event.Code)
            },
            ["parser"] = new BsonDocument { ["id"] = doc.Parser.Id },
            ["raw"] = doc.Raw,
            ["rawPreview"] = doc.RawPreview
        };

        if (doc.Actor?.User is not null)
            bson["actor"] = new BsonDocument { ["user"] = doc.Actor.User };

        if (doc.Network is not null)
        {
            var network = new BsonDocument();
            if (doc.Network.SrcIp is not null)
                network["srcIp"] = doc.Network.SrcIp;
            if (doc.Network.DstIp is not null)
                network["dstIp"] = doc.Network.DstIp;
            if (doc.Network.DstPort is not null)
                network["dstPort"] = doc.Network.DstPort.Value;
            if (doc.Network.Protocol is not null)
                network["protocol"] = doc.Network.Protocol;

            if (network.ElementCount > 0)
                bson["network"] = network;
        }

        return bson;
    }

    private static BsonValue ToBsonNullableString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? BsonNull.Value : value;
}
