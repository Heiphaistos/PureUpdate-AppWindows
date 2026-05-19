using System.Security.Principal;

namespace PureUpdate.Utils;

public static class PrivilegeHelper
{
    public static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool IsNetworkAvailable()
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var task = client.ConnectAsync("8.8.8.8", 53);
            return task.Wait(1000) && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
