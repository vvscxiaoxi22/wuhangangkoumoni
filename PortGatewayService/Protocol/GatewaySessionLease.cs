using System.Text.Json.Serialization;

namespace PortTraining.PortGatewayService.Protocol
{
    // Wire payload for gateway.session.prepared. V1.1 section 11: "控制租约字段至少
    // 包含leaseId、leaseEpoch、operationRunId、stationId、sceneInstanceId、
    // controlMode、preflightInputSequence、issuedAtUtc、expiresAtUtc" - status and
    // isSimulated are additive fields (section 19 allows receivers to ignore unknown
    // optional fields within the same major version).
    public sealed class GatewaySessionLease
    {
        [JsonPropertyName("leaseId")]
        public string LeaseId { get; set; }

        // V1.1 does not define what increments leaseEpoch; this project treats it as
        // a per-station generation counter that increases each time a new Reserved
        // lease replaces a previously released/expired one. Provisional, like the
        // other gaps flagged in PortGatewayService/README.md.
        [JsonPropertyName("leaseEpoch")]
        public ulong LeaseEpoch { get; set; }

        [JsonPropertyName("operationRunId")]
        public string OperationRunId { get; set; }

        [JsonPropertyName("stationId")]
        public string StationId { get; set; }

        [JsonPropertyName("sceneInstanceId")]
        public string SceneInstanceId { get; set; }

        [JsonPropertyName("controlMode")]
        public string ControlMode { get; set; }

        // No real or simulated realtime input stream (PT-IF-007) exists yet, so this
        // is always 0 for now - a placeholder, not a real preflight sequence number.
        [JsonPropertyName("preflightInputSequence")]
        public ulong PreflightInputSequence { get; set; }

        [JsonPropertyName("issuedAtUtc")]
        public string IssuedAtUtc { get; set; }

        [JsonPropertyName("expiresAtUtc")]
        public string ExpiresAtUtc { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("isSimulated")]
        public bool IsSimulated { get; set; }
    }
}
