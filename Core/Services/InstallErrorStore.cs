using PureUpdate.Core.Models;

namespace PureUpdate.Core.Services;

public static class InstallErrorStore
{
    private static readonly List<InstallError> _errors = [];

    public static event Action<InstallError>? OnError;

    public static IReadOnlyList<InstallError> All => _errors.AsReadOnly();

    public static void Add(InstallError error)
    {
        lock (_errors)
            _errors.Insert(0, error);
        OnError?.Invoke(error);
    }
}
