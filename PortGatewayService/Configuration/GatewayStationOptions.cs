using System.IO;
using System.Text.Json;

namespace PortTraining.PortGatewayService.Configuration
{
    // Minimal stand-in for V1.1 section 18.1's StationProfile - only the fields this
    // project currently uses. Real StationProfile loading (displayName, machineType,
    // deploymentMode, supportedControlModes, endpoints, profile ids, ...) is not
    // implemented yet.
    public sealed class GatewayStationOptions
    {
        public string StationId { get; set; } = "DEV-01";

        // Section 18.1: "allowSimulationFallback在正式考核工位必须为false". Defaults to
        // true here because appsettings.json ships as a development configuration, not
        // a real station profile - flip to false before pointing this at anything
        // resembling a formal assessment station.
        public bool AllowSimulationFallback { get; set; } = true;

        public static GatewayStationOptions LoadFromAppSettings(string baseDirectory)
        {
            var options = new GatewayStationOptions();
            var configPath = Path.Combine(baseDirectory, "appsettings.json");
            if (!File.Exists(configPath))
            {
                return options;
            }

            try
            {
                using (var document = JsonDocument.Parse(File.ReadAllText(configPath)))
                {
                    var root = document.RootElement;
                    if (root.TryGetProperty("stationId", out var stationIdElement) &&
                        stationIdElement.ValueKind == JsonValueKind.String)
                    {
                        var value = stationIdElement.GetString();
                        if (!string.IsNullOrWhiteSpace(value)) options.StationId = value;
                    }

                    if (root.TryGetProperty("allowSimulationFallback", out var fallbackElement) &&
                        (fallbackElement.ValueKind == JsonValueKind.True ||
                         fallbackElement.ValueKind == JsonValueKind.False))
                    {
                        options.AllowSimulationFallback = fallbackElement.GetBoolean();
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
