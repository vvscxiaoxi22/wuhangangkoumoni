using System;
using System.Collections.Generic;
using System.Globalization;

namespace PortTraining.YardCraneSimulator.Gateway
{
    // Request parameters for the realtime pipe's protocol.hello (V1.1 section 10.1,
    // fully specified there - no guessed fields). These values must come from a
    // gateway.session.prepare call made beforehand on the control pipe (there is no
    // scene launcher yet to automate this - see YardCraneSimulator's Gateway debug
    // client documentation for the manual test flow).
    public sealed class GatewayRealtimeHelloRequest
    {
        public string StationId;
        public string OperationRunId;
        public string SessionToken;
        public string MachineType = "YC";
        public string LeaseId;
        public string SceneInstanceId;
        public string StreamId;
        public string SceneVersion = "YC-DEBUG-CLIENT-V1";
        public string ProtocolVersion = "1.1.0";
        public string PointMapVersion = "YC-IO-0.1.0-DRAFT";
        public int RequestedFrameIntervalMs = 20;

        public string ToJson(string messageId, string senderInstanceId, string occurredAtUtc)
        {
            return "{" +
                   "\"schemaVersion\":\"1.1.0\"," +
                   "\"messageType\":\"protocol.hello\"," +
                   "\"messageId\":" + MiniJsonWriter.EscapeString(messageId) + "," +
                   "\"operationRunId\":" + MiniJsonWriter.EscapeString(OperationRunId) + "," +
                   "\"stationId\":" + MiniJsonWriter.EscapeString(StationId) + "," +
                   "\"sender\":\"YardCraneSimulator-DebugClient\"," +
                   "\"senderInstanceId\":" + MiniJsonWriter.EscapeString(senderInstanceId) + "," +
                   "\"sequence\":1," +
                   "\"occurredAtUtc\":" + MiniJsonWriter.EscapeString(occurredAtUtc) + "," +
                   "\"payload\":{" +
                   "\"sessionToken\":" + MiniJsonWriter.EscapeString(SessionToken) + "," +
                   "\"machineType\":" + MiniJsonWriter.EscapeString(MachineType) + "," +
                   "\"leaseId\":" + MiniJsonWriter.EscapeString(LeaseId) + "," +
                   "\"sceneInstanceId\":" + MiniJsonWriter.EscapeString(SceneInstanceId) + "," +
                   "\"streamId\":" + MiniJsonWriter.EscapeString(StreamId) + "," +
                   "\"sceneVersion\":" + MiniJsonWriter.EscapeString(SceneVersion) + "," +
                   "\"protocolVersion\":" + MiniJsonWriter.EscapeString(ProtocolVersion) + "," +
                   "\"pointMapVersion\":" + MiniJsonWriter.EscapeString(PointMapVersion) + "," +
                   "\"requestedFrameIntervalMs\":" + RequestedFrameIntervalMs.ToString(CultureInfo.InvariantCulture) +
                   "}" +
                   "}";
        }
    }

    // protocol.accepted / protocol.rejected - the payload shape for a rejection
    // mirrors PortGatewayService/Protocol/GatewayErrorPayload.cs (errorCode/message/retryable).
    public sealed class GatewayProtocolReply
    {
        public string MessageType;
        public string ErrorCode;
        public string ErrorMessage;
        public bool Retryable;

        public static GatewayProtocolReply Parse(string json)
        {
            if (!(MiniJsonReader.Parse(json) is Dictionary<string, object> root))
            {
                throw new FormatException("protocol reply is not a JSON object");
            }

            var reply = new GatewayProtocolReply { MessageType = GetString(root, "messageType") };

            if (root.TryGetValue("payload", out var payloadObj) && payloadObj is Dictionary<string, object> payload)
            {
                reply.ErrorCode = GetString(payload, "errorCode");
                reply.ErrorMessage = GetString(payload, "message");
                reply.Retryable = GetBool(payload, "retryable");
            }

            return reply;
        }

        private static string GetString(Dictionary<string, object> root, string key)
        {
            return root.TryGetValue(key, out var value) ? value as string : null;
        }

        private static bool GetBool(Dictionary<string, object> root, string key)
        {
            return root.TryGetValue(key, out var value) && value is bool flag && flag;
        }
    }

    // V1.1 section 10.2 equipment.inputFrame (simplified envelope, not wrapped in the
    // generic message envelope - mirrors PortGatewayService/Protocol/EquipmentInputFrame.cs).
    public sealed class GatewayEquipmentInputFrame
    {
        public string MessageType;
        public string OperationRunId;
        public string StreamId;
        public ulong Sequence;
        public string CapturedAtUtc;
        public long DataAgeMs;
        public string Quality;
        public string ControlMode;
        public bool PlcOnline;
        public bool EmergencyStopReleased;
        public bool ControlPowerOn;
        public bool HandlesCentered;
        public double GantryCommand;
        public double TrolleyCommand;
        public double HoistCommand;
        public bool HornPressed;

        public static GatewayEquipmentInputFrame Parse(string json)
        {
            if (!(MiniJsonReader.Parse(json) is Dictionary<string, object> root))
            {
                throw new FormatException("equipment.inputFrame is not a JSON object");
            }

            return new GatewayEquipmentInputFrame
            {
                MessageType = GetString(root, "messageType"),
                OperationRunId = GetString(root, "operationRunId"),
                StreamId = GetString(root, "streamId"),
                Sequence = (ulong)GetDouble(root, "sequence"),
                CapturedAtUtc = GetString(root, "capturedAtUtc"),
                DataAgeMs = (long)GetDouble(root, "dataAgeMs"),
                Quality = GetString(root, "quality"),
                ControlMode = GetString(root, "controlMode"),
                PlcOnline = GetBool(root, "plcOnline"),
                EmergencyStopReleased = GetBool(root, "emergencyStopReleased"),
                ControlPowerOn = GetBool(root, "controlPowerOn"),
                HandlesCentered = GetBool(root, "handlesCentered"),
                GantryCommand = GetDouble(root, "gantryCommand"),
                TrolleyCommand = GetDouble(root, "trolleyCommand"),
                HoistCommand = GetDouble(root, "hoistCommand"),
                HornPressed = GetBool(root, "hornPressed")
            };
        }

        private static string GetString(Dictionary<string, object> root, string key)
        {
            return root.TryGetValue(key, out var value) ? value as string : null;
        }

        private static double GetDouble(Dictionary<string, object> root, string key)
        {
            return root.TryGetValue(key, out var value) && value is double number ? number : 0;
        }

        private static bool GetBool(Dictionary<string, object> root, string key)
        {
            return root.TryGetValue(key, out var value) && value is bool flag && flag;
        }
    }
}
