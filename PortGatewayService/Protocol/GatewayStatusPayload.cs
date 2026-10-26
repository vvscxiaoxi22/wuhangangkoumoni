using System;
using System.Text.Json.Serialization;

namespace PortTraining.PortGatewayService.Protocol
{
    // V1.1 section 11 only describes gateway.status in prose ("版本、PLC在线、点表、
    // 周期、数据年龄和故障") without a worked JSON example like most other messages
    // get. These exact field names are therefore this project's provisional choice,
    // not a locked part of the standard - flag any future canonical schema here
    // before other software starts depending on this shape.
    public sealed class GatewayStatusPayload
    {
        [JsonPropertyName("gatewayVersion")]
        public string GatewayVersion { get; set; }

        [JsonPropertyName("plcOnline")]
        public bool PlcOnline { get; set; }

        [JsonPropertyName("pointMapVersion")]
        public string PointMapVersion { get; set; }

        [JsonPropertyName("pollCycleMs")]
        public double? PollCycleMs { get; set; }

        [JsonPropertyName("dataAgeMs")]
        public long? DataAgeMs { get; set; }

        [JsonPropertyName("faults")]
        public string[] Faults { get; set; } = Array.Empty<string>();

        [JsonPropertyName("notes")]
        public string Notes { get; set; }
    }
}
