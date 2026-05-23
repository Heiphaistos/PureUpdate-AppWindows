namespace PureUpdate.Core.Models;

public sealed record UpdateResult(
    bool Success,
    string Message,
    int InstalledCount = 0,
    int FailedCount    = 0,
    IReadOnlyList<string>? Errors       = null,
    int ManualCount    = 0,
    IReadOnlyList<string>? ManualErrors = null);
