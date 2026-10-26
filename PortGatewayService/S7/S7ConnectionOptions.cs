using System.IO;
using System.Text.Json;

namespace PortTraining.PortGatewayService.S7
{
    // Connection endpoint for the real S7 driver once one is chosen. V1.1 does not say
    // where a PLC's network endpoint lives - neither StationProfile (section 18.1) nor
    // PointMapManifest (section 18.4) list an IP address field; StationProfile only
    // references an undefined "plcProfileId". This is a provisional, minimal stand-in
    // loaded from appsettings.json alongside GatewayStationOptions - see
    // PortGatewayService/README.md and the decision record for this gap.
    public sealed class S7ConnectionOptions
    {
        public string IpAddress { get; set; } = "0.0.0.0";
        public int Rack { get; set; }

        // S7NetPlus's own doc: "usually it's 2 for S7300-S7400, 0 for S7-1200 and
        // S7-1500" (confirmed against the library's PLC.cs source, not assumed).
        public int Slot { get; set; }
        public int ConnectTimeoutMs { get; set; } = 3000;
        public int RetryBackoffMs { get; set; } = 5000;

        public static S7ConnectionOptions LoadFromAppSettings(string baseDirectory)
        {
            var options = new S7ConnectionOptions();
            var configPath = Path.Combine(baseDirectory, "appsettings.json");
            if (!File.Exists(configPath))
            {
                return options;
            }

            try
            {
                using (var document = JsonDocument.Parse(File.ReadAllText(configPath)))
                {
                    if (!document.RootElement.TryGetProperty("s7", out var s7Element))
                    {
                        return options;
                    }

                    if (s7Element.TryGetProperty("ipAddress", out var ip) && ip.ValueKind == JsonValueKind.String)
                    {
                        options.IpAddress = ip.GetString();
                    }

                    if (s7Element.TryGetProperty("rack", out var rack) && rack.ValueKind == JsonValueKind.Number)
                    {
                        options.Rack = rack.GetInt32();
                    }

                    if (s7Element.TryGetProperty("slot", out var slot) && slot.ValueKind == JsonValueKind.Number)
                    {
                        options.Slot = slot.GetInt32();
                    }

                    if (s7Element.TryGetProperty("connectTimeoutMs", out var timeout) && timeout.ValueKind == JsonValueKind.Number)
                    {
                        options.ConnectTimeoutMs = timeout.GetInt32();
                    }

                    if (s7Element.TryGetProperty("retryBackoffMs", out var backoff) && backoff.ValueKind == JsonValueKind.Number)
                    {
                        options.RetryBackoffMs = backoff.GetInt32();
                    }
                }
            }
            catch (JsonException)
            {
                // Fall back to the defaults above.
            }

            return options;
        }
    }
}
