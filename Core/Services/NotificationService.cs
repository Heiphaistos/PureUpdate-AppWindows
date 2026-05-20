namespace PureUpdate.Core.Services;

public static class NotificationService
{
    public static event Action<int>?    UpdatesFound;
    public static event Action<string>? RestorePointCreated;
    public static event Action?         RebootRequired;

    public static void NotifyUpdatesFound(int count)       => UpdatesFound?.Invoke(count);
    public static void NotifyRestorePoint(string msg)      => RestorePointCreated?.Invoke(msg);
    public static void NotifyRebootRequired()              => RebootRequired?.Invoke();
}
