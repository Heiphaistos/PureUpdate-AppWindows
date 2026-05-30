using PureUpdate.Core.Models;

namespace PureUpdate.Core.Services;

public static class InstallErrorStore
{
    private static readonly List<InstallError> _errors = [];

    public static event Action<InstallError>? OnError;
    public static event Action?               OnCleared;

    public static IReadOnlyList<InstallError> All   => _errors.AsReadOnly();
    public static int                         Count => _errors.Count;

    public static void Add(InstallError error)
    {
        lock (_errors)
            _errors.Insert(0, error);
        OnError?.Invoke(error);
    }

    public static void Clear()
    {
        lock (_errors)
            _errors.Clear();
        OnCleared?.Invoke();
    }
}
