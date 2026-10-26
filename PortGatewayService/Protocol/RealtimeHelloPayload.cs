using System.Text.Json.Serialization;

namespace PortTraining.PortGatewayService.Protocol
{
    // V1.1 section 10.1 protocol.hello (scene -> Gateway realtime pipe) payload -
    // unlike the control pipe's hello (see decision record), this one IS fully
    // specified: "必须包含sessionToken、stationId、operationRunId、machineType、
    // leaseId、sceneInstanceId、新生成的streamId、sceneVersion、protocolVersion、
    // pointMapVersion、requestedFrameIntervalMs". stationId and operationRunId travel
    // on the envelope itself (section 4), not duplicated here.
    public sealed class RealtimeHelloPayload
    {
        [JsonPropertyName("sessionToken")]
        public string SessionToken { get; set; }

        [JsonPropertyName("machineType")]
        public string MachineType { get; set; }

        [JsonPropertyName("leaseId")]
        public string LeaseId { get; set; }

        [JsonPropertyName("sceneInstanceId")]
        public string SceneInstanceId { get; set; }

        [JsonPropertyName("streamId")]
        public string StreamId { get; set; }

        [JsonPropertyName("sceneVersion")]
        public string SceneVersion { get; set; }

        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; }

        [JsonPropertyName("pointMapVersion")]
        public string PointMapVersion { get; set; }

        // Section 10.1: "requestedFrameIntervalMs，一期默认20".
        [JsonPropertyName("requestedFrameIntervalMs")]
        public int? RequestedFrameIntervalMs { get; set; }
    }
}
