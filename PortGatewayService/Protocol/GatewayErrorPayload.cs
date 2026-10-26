using System.Text.Json.Serialization;

namespace PortTraining.PortGatewayService.Protocol
{
    // Reuses the section-15 error shape (code/message/retryable) - defined there as
    // "HTTP错误结构" but the error codes themselves (CONTRACT_VALIDATION_FAILED,
    // PLC_OFFLINE, ...) are written generically and several only make sense for
    // Named Pipe control messages, so this carries the same fields as this project's
    // Named Pipe rejection payload instead of inventing a parallel code list.
    public sealed class GatewayErrorPayload
    {
        [JsonPropertyName("errorCode")]
        public string ErrorCode { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("retryable")]
        public bool Retryable { get; set; }
    }
}
