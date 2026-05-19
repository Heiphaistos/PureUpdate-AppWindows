using PureUpdate.Core.Models;
using PureUpdate.Utils;

namespace PureUpdate.Core.Providers;

public sealed class ChocoManager : CliProviderBase, IUpdateProvider
{
    public string Name        => "Chocolatey";
    public string Description => "Gestionnaire de paquets Chocolatey";
    public string Icon        => "";
    public bool IsAvailable   => IsCommandAvailable("choco");

    public async Task<List<UpdateItem>> ScanAsync(CancellationToken ct = default)
    {
        var items = new List<UpdateItem>();
        if (!IsAvailable) return items;

        Logger.Info("[Chocolatey] Recherche des mises à jour...");

        try
        {
            var output = await RunAsync("choco", "outdated --no-color -r", ct: ct);
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length < 2) continue;

                string name      = parts[0].Trim();
                string version   = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                string available = parts.Length > 2 ? parts[2].Trim() : string.Empty;
                bool pinned      = parts.Length > 3 && parts[3].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

                if (pinned || string.IsNullOrWhiteSpace(name)) continue;

                items.Add(new UpdateItem
                {
                    Id               = name,
                    Title            = name,
                    Version          = version,
                    AvailableVersion = available,
                    Provider         = Name,
                });
            }
            Logger.Info($"[Chocolatey] {items.Count} mises à jour trouvées");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Chocolatey] Scan échoué: {ex.Message}"); }

        return items;
    }

    public async Task<UpdateResult> InstallAsync(
        List<UpdateItem> items,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsAvailable)
            return new UpdateResult(false, "Chocolatey non disponible");

        progress?.Report("Installation de toutes les mises à jour Chocolatey...");

        try
        {
            var output = await RunAsync("choco", "upgrade all -y --no-color", progress, ct);
            bool success = !output.Contains("Failures", StringComparison.OrdinalIgnoreCase);
            Logger.Info("[Chocolatey] Mise à jour terminée");
            return new UpdateResult(success, success ? "Mise à jour réussie" : "Erreurs lors de la mise à jour", items.Count(i => i.IsSelected));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Logger.Error($"[Chocolatey] Install échoué: {ex.Message}");
            return new UpdateResult(false, ex.Message);
        }
    }
}
