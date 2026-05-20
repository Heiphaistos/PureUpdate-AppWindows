using PureUpdate.Core.Models;

namespace PureUpdate.Core.Providers;

public interface ISelfManagedProvider
{
    bool CanInstallSelf   { get; }
    bool CanUninstallSelf { get; }

    Task<bool>           InstallSelfAsync  (IProgress<string>? progress, CancellationToken ct);
    Task<bool>           UninstallSelfAsync(IProgress<string>? progress, CancellationToken ct);
    Task<List<HistoryItem>> GetInstalledPackagesAsync(CancellationToken ct);
}
