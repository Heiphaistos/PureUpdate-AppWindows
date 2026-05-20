using PureUpdate.Core.Models;
using PureUpdate.Utils;

namespace PureUpdate.Core.Providers;

public sealed class WingetManager : CliProviderBase, IUpdateProvider, ISelfManagedProvider
{
    public string Name        => "Winget";
    public string Description => "Windows Package Manager";
    public string AccentHex   => "#00B7FF";

    private bool? _available;
    public bool IsAvailable => _available ??= IsCommandAvailable("winget");

    public bool CheckAvailability()
    {
        _available = IsCommandAvailable("winget");
        return _available.Value;
    }

    // --- IUpdateProvider ---

    public async Task<List<UpdateItem>> ScanAsync(CancellationToken ct = default)
    {
        var items = new List<UpdateItem>();
        if (!IsAvailable) return items;

        Logger.Info("[Winget] Scan des mises à jour...");
        try
        {
            var output = await RunAsync("winget",
                "upgrade --include-unknown --accept-source-agreements --disable-interactivity",
                ct: ct);
            items = ParseUpgradeTable(output);
            Logger.Info($"[Winget] {items.Count} mise(s) à jour détectée(s)");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Winget] Scan: {ex.Message}"); }
        return items;
    }

    public async Task<UpdateResult> InstallAsync(
        List<UpdateItem>   items,
        IProgress<string>? progress = null,
        CancellationToken  ct       = default)
    {
        if (!IsAvailable) return new UpdateResult(false, "Winget non disponible");

        int installed = 0, failed = 0;
        var errors = new List<string>();

        foreach (var item in items.Where(i => i.IsSelected))
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Installation: {item.Title}...");
            try
            {
                var output = await RunAsync("winget",
                    $"upgrade --id \"{item.Id}\" --silent --accept-package-agreements --accept-source-agreements",
                    progress, ct);

                if (output.Contains("Successfully installed", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("No applicable upgrade found", StringComparison.OrdinalIgnoreCase))
                    installed++;
                else { failed++; errors.Add(item.Title); }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { failed++; errors.Add(item.Title); Logger.Error($"[Winget] {item.Title}: {ex.Message}"); }
        }

        return new UpdateResult(failed == 0, $"{installed} installées, {failed} erreurs", installed, failed, errors);
    }

    // --- ISelfManagedProvider ---

    public bool CanInstallSelf   => false; // Winget = système Windows, pas auto-installable
    public bool CanUninstallSelf => false;

    public Task<bool> InstallSelfAsync(IProgress<string>? progress, CancellationToken ct)
        => Task.FromResult(false);

    public Task<bool> UninstallSelfAsync(IProgress<string>? progress, CancellationToken ct)
        => Task.FromResult(false);

    public async Task<List<HistoryItem>> GetInstalledPackagesAsync(CancellationToken ct = default)
    {
        var items = new List<HistoryItem>();
        if (!IsAvailable) return items;

        try
        {
            var output = await RunAsync("winget",
                "list --source winget --accept-source-agreements --disable-interactivity", ct: ct);
            items = ParseListTable(output, "Winget");
            Logger.Info($"[Winget] {items.Count} paquets installés");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Winget] GetInstalled: {ex.Message}"); }
        return items;
    }

    // --- Parsers ---

    private List<UpdateItem> ParseUpgradeTable(string output)
    {
        var items = new List<UpdateItem>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int headerIdx = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("Available", StringComparison.OrdinalIgnoreCase) &&
                lines[i].Contains("Version",   StringComparison.OrdinalIgnoreCase) &&
                lines[i].Contains("Id",        StringComparison.OrdinalIgnoreCase))
            { headerIdx = i; break; }
        }
        if (headerIdx < 0 || headerIdx + 2 >= lines.Length) return items;

        var (_, idPos, verPos, availPos, srcPos) = ParseWingetHeader(lines[headerIdx]);
        if (idPos < 0 || verPos < 0 || availPos < 0) return items;

        for (int i = headerIdx + 2; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length < verPos) continue;
            if (line.TrimStart().StartsWith("upgrade", StringComparison.OrdinalIgnoreCase)) break;

            try
            {
                string name      = idPos > 0 ? line[..idPos].Trim() : line.Trim();
                string id        = verPos > idPos && line.Length >= verPos ? line[idPos..verPos].Trim() : name;
                string version   = availPos > verPos && line.Length >= availPos ? line[verPos..availPos].Trim() : "";
                string available = srcPos > availPos && line.Length >= srcPos
                    ? line[availPos..srcPos].Trim()
                    : line.Length > availPos ? line[availPos..].Trim() : "";

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id)) continue;

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

    private static List<HistoryItem> ParseListTable(string output, string provider)
    {
        var items = new List<HistoryItem>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int headerIdx = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("Name", StringComparison.OrdinalIgnoreCase) &&
                lines[i].Contains("Version", StringComparison.OrdinalIgnoreCase))
            { headerIdx = i; break; }
        }
        if (headerIdx < 0 || headerIdx + 2 >= lines.Length) return items;

        string header = lines[headerIdx];
        int idPos  = header.IndexOf(" Id ",      StringComparison.OrdinalIgnoreCase);
        int verPos = header.IndexOf(" Version ", StringComparison.OrdinalIgnoreCase);
        if (idPos < 0) idPos = header.IndexOf("Id", StringComparison.OrdinalIgnoreCase);
        if (verPos < 0) return items;

        for (int i = headerIdx + 2; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length < verPos) continue;

            string name    = idPos > 0 && line.Length > idPos ? line[..idPos].Trim() : line.Trim();
            string id      = verPos > idPos && line.Length >= verPos ? line[idPos..verPos].Trim() : name;
            string version = line.Length > verPos ? line[verPos..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "" : "";

            if (string.IsNullOrWhiteSpace(name)) continue;

            items.Add(new HistoryItem
            {
                Title    = name,
                Id       = id,
                Version  = version,
                Provider = provider,
                Status   = HistoryStatus.Success,
                Date     = DateTime.MinValue,
            });
        }
        return items;
    }
}
