using PureUpdate.Core.Models;
using PureUpdate.Utils;

namespace PureUpdate.Core.Providers;

public sealed class ScoopManager : CliProviderBase, IUpdateProvider, ISelfManagedProvider, IUninstallProvider
{
    public string Name        => "Scoop";
    public string Description => "Gestionnaire de paquets Scoop";
    public string AccentHex   => "#5CB85C";

    private bool? _available;
    public bool IsAvailable => _available ??= CheckScoopAvailable();

    public bool CheckAvailability()
    {
        _available = CheckScoopAvailable();
        return _available.Value;
    }

    private static bool CheckScoopAvailable()
    {
        // Filesystem check — rapide, pas de processus, pas de deadlock
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var scoopEnv    = Environment.GetEnvironmentVariable("SCOOP", EnvironmentVariableTarget.User)
                         ?? Path.Combine(userProfile, "scoop");
        return File.Exists(Path.Combine(scoopEnv, "shims", "scoop.cmd"))
            || File.Exists(Path.Combine(scoopEnv, "shims", "scoop.ps1"))
            || File.Exists(Path.Combine(userProfile, "scoop", "shims", "scoop.cmd"));
    }

    // --- IUpdateProvider ---

    public async Task<List<UpdateItem>> ScanAsync(CancellationToken ct = default)
    {
        var items = new List<UpdateItem>();
        if (!IsAvailable) return items;

        Logger.Info("[Scoop] Scan des mises à jour...");
        try
        {
            await RunPs("scoop update", ct: ct);
            var output = await RunPs("scoop status", ct: ct);
            items = ParseScoopStatus(output);
            Logger.Info($"[Scoop] {items.Count} mise(s) à jour détectée(s)");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Scoop] Scan: {ex.Message}"); }
        return items;
    }

    public async Task<UpdateResult> InstallAsync(
        List<UpdateItem>   items,
        IProgress<string>? progress = null,
        CancellationToken  ct       = default)
    {
        if (!IsAvailable) return new UpdateResult(false, "Scoop non disponible");

        progress?.Report("Mise à jour de tous les paquets Scoop...");
        try
        {
            var output = await RunPs("scoop update *", progress, ct);
            Logger.Info("[Scoop] Update terminé");
            return new UpdateResult(true, "Mise à jour réussie", items.Count(i => i.IsSelected));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Scoop] Install: {ex.Message}"); return new UpdateResult(false, ex.Message); }
    }

    // --- ISelfManagedProvider ---

    public bool CanInstallSelf   => !IsAvailable;
    public bool CanUninstallSelf => IsAvailable;

    public async Task<bool> InstallSelfAsync(IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report("Installation de Scoop...");
        Logger.Info("[Scoop] Auto-installation...");
        try
        {
            // -RunAsAdmin requis quand PureUpdate tourne en administrateur
            var script = "Set-ExecutionPolicy RemoteSigned -Scope CurrentUser -Force; " +
                         "iex \"& {$(irm get.scoop.sh)} -RunAsAdmin\"";
            var output = await RunAsync("powershell.exe",
                $"-NoProfile -NonInteractive -Command \"{script}\"", progress, ct);

            _available = null;
            bool ok = IsAvailable;
            Logger.Info($"[Scoop] Installation: {(ok ? "OK" : "Échec")}");
            return ok;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Scoop] InstallSelf: {ex.Message}"); return false; }
    }

    public async Task<bool> UninstallSelfAsync(IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report("Désinstallation de Scoop...");
        Logger.Info("[Scoop] Auto-désinstallation...");
        try
        {
            await RunPs("scoop uninstall scoop", progress, ct);
            _available = null;
            Logger.Info("[Scoop] Désinstallation terminée");
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Scoop] UninstallSelf: {ex.Message}"); return false; }
    }

    public async Task<List<HistoryItem>> GetInstalledPackagesAsync(CancellationToken ct = default)
    {
        var items = new List<HistoryItem>();
        if (!IsAvailable) return items;

        try
        {
            var output = await RunPs("scoop list", ct: ct);
            items = ParseScoopList(output);
            Logger.Info($"[Scoop] {items.Count} paquets installés");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Scoop] GetInstalled: {ex.Message}"); }
        return items;
    }

    // --- IUninstallProvider ---

    public async Task<UninstallResult> UninstallPackagesAsync(
        List<HistoryItem>  items,
        IProgress<string>? progress,
        CancellationToken  ct)
    {
        if (!IsAvailable) return new UninstallResult(false, "Scoop non disponible");

        int uninstalled = 0, failed = 0;
        var errors = new List<string>();

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Désinstallation: {item.Title}...");
            try
            {
                var (_, exitCode) = await RunWithCodeAsync("powershell.exe",
                    $"-NoProfile -NonInteractive -Command \"scoop uninstall {item.Id} --purge\"",
                    progress, ct);

                if (exitCode == 0) { uninstalled++; Logger.Info($"[Scoop] Désinstallé: {item.Title}"); }
                else { failed++; errors.Add(item.Title); Logger.Warn($"[Scoop] Échec désinstall {item.Title}: exit {exitCode}"); }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { failed++; errors.Add(item.Title); Logger.Error($"[Scoop] Désinstall {item.Title}: {ex.Message}"); }
        }

        return new UninstallResult(failed == 0, $"{uninstalled} désinstallé(s), {failed} erreur(s)", uninstalled, failed, errors);
    }

    // --- Helpers ---

    private static Task<string> RunPs(string cmd,
        IProgress<string>? progress = null, CancellationToken ct = default)
        => RunAsync("powershell.exe",
            $"-NoProfile -NonInteractive -Command \"{cmd}\"",
            progress, ct);

    private static List<UpdateItem> ParseScoopStatus(string output)
    {
        var items = new List<UpdateItem>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int headerIdx = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("Latest", StringComparison.OrdinalIgnoreCase) &&
                lines[i].Contains("Installed", StringComparison.OrdinalIgnoreCase))
            { headerIdx = i; break; }
        }
        if (headerIdx < 0) return items;

        string header  = lines[headerIdx];
        int instCol    = header.IndexOf("Installed", StringComparison.OrdinalIgnoreCase);
        int latestCol  = header.IndexOf("Latest",    StringComparison.OrdinalIgnoreCase);
        if (instCol < 0 || latestCol < 0) return items;

        for (int i = headerIdx + 2; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("---")) continue;
            if (line.Length < latestCol) continue;

            string name    = instCol > 0 ? line[..instCol].Trim() : line.Trim();
            string version = latestCol > instCol && line.Length >= latestCol ? line[instCol..latestCol].Trim() : "";
            string latest  = line.Length > latestCol ? line[latestCol..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "" : "";

            if (string.IsNullOrWhiteSpace(name)) continue;

            items.Add(new UpdateItem
            {
                Id               = name,
                Title            = name,
                Version          = version,
                AvailableVersion = latest,
                Provider         = "Scoop",
            });
        }
        return items;
    }

    private static List<HistoryItem> ParseScoopList(string output)
    {
        var items = new List<HistoryItem>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int headerIdx = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("Version", StringComparison.OrdinalIgnoreCase) &&
                lines[i].TrimStart().StartsWith("Name", StringComparison.OrdinalIgnoreCase))
            { headerIdx = i; break; }
        }
        if (headerIdx < 0) return items;

        string header = lines[headerIdx];
        int verCol = header.IndexOf("Version", StringComparison.OrdinalIgnoreCase);
        if (verCol < 0) return items;

        for (int i = headerIdx + 2; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("---")) continue;
            if (line.Length < verCol) continue;

            string name    = line[..verCol].Trim();
            string version = line.Length > verCol ? line[verCol..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "" : "";

            if (string.IsNullOrWhiteSpace(name)) continue;

            items.Add(new HistoryItem
            {
                Title    = name,
                Id       = name,
                Version  = version,
                Provider = "Scoop",
                Status   = HistoryStatus.Success,
                Date     = DateTime.MinValue,
            });
        }
        return items;
    }
}
