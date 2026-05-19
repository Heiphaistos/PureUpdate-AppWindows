using PureUpdate.Core.Models;

namespace PureUpdate.Core.Providers;

public interface IUpdateProvider
{
    string Name        { get; }
    string Description { get; }
    string Icon        { get; }
    bool IsAvailable   { get; }

    Task<List<UpdateItem>> ScanAsync(CancellationToken ct = default);

    Task<UpdateResult> InstallAsync(
        List<UpdateItem> items,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}
