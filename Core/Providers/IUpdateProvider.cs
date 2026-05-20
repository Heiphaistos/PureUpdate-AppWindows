using PureUpdate.Core.Models;

namespace PureUpdate.Core.Providers;

public interface IUpdateProvider
{
    string Name        { get; }
    string Description { get; }
    string AccentHex   { get; }
    bool   IsAvailable { get; }

    bool CheckAvailability();

    Task<List<UpdateItem>> ScanAsync(CancellationToken ct = default);

    Task<UpdateResult> InstallAsync(
        List<UpdateItem>   items,
        IProgress<string>? progress = null,
        CancellationToken  ct       = default);
}
