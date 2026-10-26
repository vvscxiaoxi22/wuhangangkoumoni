namespace PortTraining.PortGatewayService.Protocol
{
    // Limits and constants from LM-Wiki/02-知识层-编译层/跨软件接口与参数标准-v1.1.md section 3.2/4.
    public static class ProtocolConstants
    {
        public const string SchemaVersion = "1.1.0";

        // Section 3.2: "控制和业务消息最大1 MiB".
        public const int MaxControlFrameBytes = 1 * 1024 * 1024;

        // Section 3.2: "实时设备帧最大256 KiB".
        public const int MaxRealtimeFrameBytes = 256 * 1024;
    }
}
