using System.Text.Json.Serialization;

namespace PortTraining.PortGatewayService.Protocol
{
    // Payload for gateway.lease.released (V1.1 section 11 names the message pair but
    // not its fields, same gap pattern as the other messages in this project - see
    // PortGatewayService协议扩展决策记录-v1.0.md 2.12).
    public sealed class GatewayLeaseReleasedPayload
    {
        [JsonPropertyName("leaseId")]
        public string LeaseId { get; set; }
    }
}
