using System;
using System.Text;

namespace PortTraining.PortGatewayService.Protocol
{
    // Pipe name rules from V1.1 section 3.2: fixed prefixes plus a normalized stationId
    // ("转换为小写，只保留a-z、0-9和短横线"). The runtime NamedPipeServerStream/
    // NamedPipeClientStream APIs handle the "\\.\pipe\{pipeName}" prefix themselves.
    public static class PipeNaming
    {
        public static string NormalizeStationId(string stationId)
        {
            if (stationId == null) throw new ArgumentNullException(nameof(stationId));

            var builder = new StringBuilder(stationId.Length);
            foreach (var character in stationId.ToLowerInvariant())
            {
                if ((character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9') ||
                    character == '-')
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        public static string BuildControlPipeName(string stationId)
        {
            return "pt.gateway.control." + NormalizeStationId(stationId);
        }

        public static string BuildRealtimePipeName(string stationId)
        {
            return "pt.gateway.realtime." + NormalizeStationId(stationId);
        }
    }
}
