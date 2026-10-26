using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortTraining.PortGatewayService.Protocol
{
    // Shared JSON options for every wire type in this project: camelCase field names
    // (V1.1 section 5.2) and omitting unset optional fields instead of writing null
    // (V1.1 section 4: "未使用的可选字段直接省略，不使用空字符串表示没有值").
    public static class GatewayJsonOptions
    {
        public static readonly JsonSerializerOptions Default = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}
