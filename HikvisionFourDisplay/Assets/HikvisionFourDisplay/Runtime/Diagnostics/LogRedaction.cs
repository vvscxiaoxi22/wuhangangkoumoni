namespace HikvisionFourDisplay.Runtime.Diagnostics
{
    public static class LogRedaction
    {
        public static string MaskHost(string host)
        {
            if (string.IsNullOrEmpty(host)) return "unset";

            var parts = host.Split('.');
            if (parts.Length == 4) return $"{parts[0]}.{parts[1]}.*.*";

            return host.Length <= 4 ? "***" : host.Substring(0, 2) + "***";
        }

        public static string MaskEndpoint(string host, int port, string channel)
        {
            return $"{MaskHost(host)}:{port}/{channel}";
        }
    }
}
