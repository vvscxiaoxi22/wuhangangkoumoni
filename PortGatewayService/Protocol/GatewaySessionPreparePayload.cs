using System.Text.Json.Serialization;

namespace PortTraining.PortGatewayService.Protocol
{
    // Request payload for gateway.session.prepare (V1.1 section 11: "gateway.session.prepare
    // 请求必须包含operationRunId、ownerInstanceId、sceneInstanceId、sessionTokenHash、
    // workOrderSnapshotHash、controlMode、sceneVersion、pointMapVersion和请求租约时长；
    // 不得包含原始会话令牌"). operationRunId and stationId are not repeated here - section 4
    // already makes them required top-level envelope fields for run/station-scoped messages.
    public sealed class GatewaySessionPreparePayload
    {
        [JsonPropertyName("ownerInstanceId")]
        public string OwnerInstanceId { get; set; }

        [JsonPropertyName("sceneInstanceId")]
        public string SceneInstanceId { get; set; }

        [JsonPropertyName("sessionTokenHash")]
        public string SessionTokenHash { get; set; }

        [JsonPropertyName("workOrderSnapshotHash")]
        public string WorkOrderSnapshotHash { get; set; }

        // ControlMode enum (section 6): Local | Remote.
        [JsonPropertyName("controlMode")]
        public string ControlMode { get; set; }

        [JsonPropertyName("sceneVersion")]
        public string SceneVersion { get; set; }

        [JsonPropertyName("pointMapVersion")]
        public string PointMapVersion { get; set; }

        // V1.1 only says "请求租约时长" in prose with no field name; this follows
        // section 5.3's "持续时长统一使用整数毫秒字段后缀Ms" naming rule. Provisional,
        // like the other gaps flagged in PortGatewayService/README.md.
        [JsonPropertyName("requestedLeaseDurationMs")]
        public long? RequestedLeaseDurationMs { get; set; }
    }
}
