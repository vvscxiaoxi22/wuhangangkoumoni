using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace PortTraining.PortGatewayService.Security
{
    // V1.1 section 17: "Named Pipe设置Windows ACL，仅允许当前交互用户、受信服务账号和
    // 管理员访问". "受信服务账号" has no concrete identity yet - PortGatewayService does
    // not have a defined Windows Service / service-account deployment story in this
    // project, so only the current process's user and BUILTIN\Administrators are
    // modeled for now. Revisit once a real service-account deployment exists.
    public static class GatewayPipeSecurity
    {
        public static PipeSecurity BuildDefault()
        {
            var security = new PipeSecurity();

            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser != null)
            {
                security.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.FullControl, AccessControlType.Allow));
            }

            var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.AddAccessRule(new PipeAccessRule(administrators, PipeAccessRights.FullControl, AccessControlType.Allow));

            return security;
        }
    }
}
