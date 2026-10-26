using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortTraining.PortGatewayService.Protocol
{
    // Wire shape from LM-Wiki/02-知识层-编译层/跨软件接口与参数标准-v1.1.md section 4
    // ("通用消息包络"). Field names, required/optional split and the "no empty-string
    // placeholder" rule follow that section exactly; this type does not add fields.
    public sealed class MessageEnvelope
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; set; } = ProtocolConstants.SchemaVersion;

        [JsonPropertyName("messageType")]
        public string MessageType { get; set; }

        [JsonPropertyName("messageId")]
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("correlationId")]
        public string CorrelationId { get; set; }

        [JsonPropertyName("operationId")]
        public string OperationId { get; set; }

        [JsonPropertyName("operationRunId")]
        public string OperationRunId { get; set; }

        [JsonPropertyName("workOrderId")]
        public string WorkOrderId { get; set; }

        [JsonPropertyName("stationId")]
        public string StationId { get; set; }

        [JsonPropertyName("sender")]
        public string Sender { get; set; }

        [JsonPropertyName("senderInstanceId")]
        public string SenderInstanceId { get; set; }

        [JsonPropertyName("sequence")]
        public ulong Sequence { get; set; }

        [JsonPropertyName("occurredAtUtc")]
        public string OccurredAtUtc { get; set; }

        [JsonPropertyName("payload")]
        public JsonElement Payload { get; set; } = JsonDocument.Parse("{}").RootElement;
    }

    public static class EnvelopeSerializer
    {
        public static byte[] Serialize(MessageEnvelope envelope)
        {
            return JsonSerializer.SerializeToUtf8Bytes(envelope, GatewayJsonOptions.Default);
        }

        public static MessageEnvelope Deserialize(byte[] utf8Json)
        {
            return JsonSerializer.Deserialize<MessageEnvelope>(utf8Json, GatewayJsonOptions.Default);
        }

        // Section 4: "occurredAtUtc ... UTC RFC3339，精确到毫秒，以Z结尾". The default
        // System.Text.Json DateTimeOffset format uses a numeric offset (+00:00), so this
        // formats explicitly instead of relying on built-in DateTimeOffset conversion.
        public static string FormatOccurredAtUtc(DateTimeOffset instant)
        {
            return instant.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }
    }
}
