namespace PureUpdate.Core.Models;

public sealed record UninstallResult(
    bool Success,
    string Message,
    int UninstalledCount = 0,
    int FailedCount      = 0,
    IReadOnlyList<string>? Errors = null);
