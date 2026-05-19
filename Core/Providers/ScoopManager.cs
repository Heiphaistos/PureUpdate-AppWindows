using PureUpdate.Core.Models;
using PureUpdate.Utils;

namespace PureUpdate.Core.Providers;

public sealed class ScoopManager : CliProviderBase, IUpdateProvider
{
    public string Name        => "Scoop";
    public string Description => "Gestionnaire de paquets Scoop";
    public string Icon        => "";
    public bool IsAvailable   => IsCommandAvailable("scoop");

    public async Task<List<UpdateItem>> ScanAsync(CancellationToken ct = default)
    {
        var items = new List<UpdateItem>();
        if (!IsAvailable) return items;

        Logger.Info("[Scoop] Recherche des mises à jour...");

        try
        {
            await RunAsync("scoop", "update", ct: ct);
            var output = await RunAsync("scoop", "status", ct: ct);
            items = ParseScoopStatus(output);
            Logger.Info($"[Scoop] {items.Count} mises à jour trouvées");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Scoop] Scan échoué: {ex.Message}"); }

        return items;
    }

    public async Task<UpdateResult> InstallAsync(
        List<UpdateItem> items,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsAvailable)
            return new UpdateResult(false, "Scoop non disponible");

        progress?.Report("Mise à jour de tous les paquets Scoop...");

        try
        {
            var output = await RunAsync("scoop", "update *", progress, ct);
            Logger.Info("[Scoop] Mise à jour terminée");
            return new UpdateResult(true, "Mise à jour Scoop réussie", items.Count(i => i.IsSelected));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Logger.Error($"[Scoop] Install échoué: {ex.Message}");
            return new UpdateResult(false, ex.Message);
        }
    }

    private static List<UpdateItem> ParseScoopStatus(string output)
    {
        var items = new List<UpdateItem>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        bool inTable = false;

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("Name", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("Installed Version", StringComparison.OrdinalIgnoreCase))
            {
                inTable = true;
                continue;
            }
            if (!inTable || line.TrimStart().StartsWith("---")) continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            string name      = parts[0];
            string installed = parts.Length > 1 ? parts[1] : string.Empty;
            string latest    = parts.Length > 2 ? parts[2] : string.Empty;

            if (string.IsNullOrWhiteSpace(name)) continue;

            items.Add(new UpdateItem
            {
                Id               = name,
                Title            = name,
                Version          = installed,
                AvailableVersion = latest,
                Provider         = "Scoop",
            });
        }

        return items;
    }
}
