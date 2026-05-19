using PureUpdate.Core.Models;
using PureUpdate.Utils;

namespace PureUpdate.Core.Providers;

public sealed class WingetManager : CliProviderBase, IUpdateProvider
{
    public string Name        => "Winget";
    public string Description => "Windows Package Manager";
    public string Icon        => "";
    public bool IsAvailable   => IsCommandAvailable("winget");

    public async Task<List<UpdateItem>> ScanAsync(CancellationToken ct = default)
    {
        var items = new List<UpdateItem>();
        if (!IsAvailable) return items;

        Logger.Info("[Winget] Recherche des mises à jour...");

        try
        {
            var output = await RunAsync("winget", "upgrade --include-unknown", ct: ct);
            items = ParseWingetOutput(output);
            Logger.Info($"[Winget] {items.Count} mises à jour trouvées");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Winget] Scan échoué: {ex.Message}"); }

        return items;
    }

    public async Task<UpdateResult> InstallAsync(
        List<UpdateItem> items,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsAvailable)
            return new UpdateResult(false, "Winget non disponible");

        int installed = 0;
        int failed    = 0;
        var errors    = new List<string>();

        foreach (var item in items.Where(i => i.IsSelected))
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Installation de {item.Title}...");
            try
            {
                var output = await RunAsync(
                    "winget",
                    $"upgrade --id \"{item.Id}\" --silent --accept-package-agreements --accept-source-agreements",
                    progress, ct);

                if (output.Contains("Successfully installed", StringComparison.OrdinalIgnoreCase))
                    installed++;
                else
                {
                    failed++;
                    errors.Add(item.Title);
                    Logger.Warn($"[Winget] Échec pour {item.Title}");
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                failed++;
                errors.Add(item.Title);
                Logger.Error($"[Winget] Erreur {item.Title}: {ex.Message}");
            }
        }

        return new UpdateResult(failed == 0, $"{installed} installées, {failed} erreurs", installed, failed, errors);
    }

    private static List<UpdateItem> ParseWingetOutput(string output)
    {
        var items = new List<UpdateItem>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int separatorIdx = Array.FindIndex(lines, l => l.TrimStart().StartsWith("---"));
        if (separatorIdx < 0) return items;

        string header = lines[separatorIdx - 1];
        int versionCol   = header.IndexOf("Version",   StringComparison.OrdinalIgnoreCase);
        int availableCol = header.IndexOf("Available",  StringComparison.OrdinalIgnoreCase);
        int sourceCol    = header.IndexOf("Source",     StringComparison.OrdinalIgnoreCase);

        for (int i = separatorIdx + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length < availableCol || line.TrimStart().StartsWith("upgrades available", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                string name      = line[..versionCol].Trim();
                string version   = versionCol < availableCol ? line[versionCol..availableCol].Trim() : string.Empty;
                string available = availableCol < sourceCol && sourceCol <= line.Length
                    ? line[availableCol..sourceCol].Trim()
                    : string.Empty;
                string id = name;

                if (string.IsNullOrWhiteSpace(name)) continue;

                items.Add(new UpdateItem
                {
                    Id               = id,
                    Title            = name,
                    Version          = version,
                    AvailableVersion = available,
                    Provider         = "Winget",
                });
            }
            catch { }
        }

        return items;
    }
}
