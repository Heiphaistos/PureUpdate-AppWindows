using Microsoft.Win32;
using PureUpdate.Utils;

namespace PureUpdate.Core.Services;

public static class RebootRequiredService
{
    private const string WuKey      = @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired";
    private const string CbsKey     = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending";
    private const string SessionKey = @"SYSTEM\CurrentControlSet\Control\Session Manager";

    public static bool IsRebootRequired()
    {
        try
        {
            if (KeyExists(Registry.LocalMachine, WuKey))  return true;
            if (KeyExists(Registry.LocalMachine, CbsKey)) return true;
            if (PendingFileRenameExists())                 return true;
            return false;
        }
        catch (Exception ex)
        {
            Logger.Warn($"[Reboot] Check: {ex.Message}");
            return false;
        }
    }

    private static bool KeyExists(RegistryKey root, string path)
    {
        using var key = root.OpenSubKey(path);
        return key is not null;
    }

    private static bool PendingFileRenameExists()
    {
        using var key = Registry.LocalMachine.OpenSubKey(SessionKey);
        if (key is null) return false;
        var val = key.GetValue("PendingFileRenameOperations");
        if (val is string[] arr) return arr.Length > 0;
        return val is not null;
    }
}
