using System.Text.Json.Serialization;

namespace PortTraining.PortGatewayService.Protocol
{
    // V1.1 section 4: "除20毫秒实时帧可使用精简包络外，所有JSON消息使用以下结构" - this is
    // one of those 20ms real-time frames, so it deliberately does NOT reuse MessageEnvelope
    // (no messageId/correlationId/sender/senderInstanceId here). Field set matches section
    // 10.2's EquipmentInputFrame JSON example exactly, minus machineInputs (kept as an
    // empty object placeholder - no real/simulated machine-specific inputs exist yet).
    public sealed class EquipmentInputFrame
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; set; } = ProtocolConstants.SchemaVersion;

        [JsonPropertyName("messageType")]
        public string MessageType { get; set; } = "equipment.inputFrame";

        [JsonPropertyName("operationRunId")]
        public string OperationRunId { get; set; }

        [JsonPropertyName("streamId")]
        public string StreamId { get; set; }

        [JsonPropertyName("sequence")]
        public ulong Sequence { get; set; }

        [JsonPropertyName("capturedAtUtc")]
        public string CapturedAtUtc { get; set; }

        [JsonPropertyName("dataAgeMs")]
        public long DataAgeMs { get; set; }

        [JsonPropertyName("quality")]
        public string Quality { get; set; }

        [JsonPropertyName("controlMode")]
        public string ControlMode { get; set; }

        [JsonPropertyName("plcOnline")]
        public bool PlcOnline { get; set; }

        [JsonPropertyName("emergencyStopReleased")]
        public bool EmergencyStopReleased { get; set; }

        [JsonPropertyName("controlPowerOn")]
        public bool ControlPowerOn { get; set; }

        [JsonPropertyName("handlesCentered")]
        public bool HandlesCentered { get; set; }

        [JsonPropertyName("gantryCommand")]
        public double GantryCommand { get; set; }

        [JsonPropertyName("trolleyCommand")]
        public double TrolleyCommand { get; set; }

        [JsonPropertyName("hoistCommand")]
        public double HoistCommand { get; set; }

        [JsonPropertyName("hornPressed")]
        public bool HornPressed { get; set; }

        [JsonPropertyName("machineInputs")]
        public object MachineInputs { get; set; } = new object();
    }
}
