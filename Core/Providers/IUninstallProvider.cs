using PureUpdate.Core.Models;

namespace PureUpdate.Core.Providers;

public interface IUninstallProvider
{
    Task<List<HistoryItem>> GetInstalledPackagesAsync(CancellationToken ct = default);

    Task<UninstallResult> UninstallPackagesAsync(
        List<HistoryItem>  items,
        IProgress<string>? progress,
        CancellationToken  ct);
}
