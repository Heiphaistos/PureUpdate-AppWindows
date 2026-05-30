using System.Diagnostics;
using PureUpdate.Core.Models;
using PureUpdate.Utils;

namespace PureUpdate.Core.Providers;

public sealed class ChocoManager : CliProviderBase, IUpdateProvider, ISelfManagedProvider, IUninstallProvider
{
    public string Name        => "Chocolatey";
    public string Description => "Gestionnaire de paquets Chocolatey";
    public string AccentHex   => "#FF6900";

    private bool? _available;
    public bool IsAvailable => _available ??= IsCommandAvailable("choco");

    public bool CheckAvailability()
    {
        _available = IsCommandAvailable("choco");
        return _available.Value;
    }

    // --- IUpdateProvider ---

    public async Task<List<UpdateItem>> ScanAsync(CancellationToken ct = default)
    {
        var items = new List<UpdateItem>();
        if (!IsAvailable) return items;

        Logger.Info("[Chocolatey] Scan des mises à jour...");
        try
        {
            var output = await RunAsync("choco", "outdated --no-color -r", ct: ct);
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length < 3) continue;
                string name    = parts[0].Trim();
                string ver     = parts[1].Trim();
                string newVer  = parts[2].Trim();
                bool   pinned  = parts.Length > 3 && parts[3].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                if (pinned || string.IsNullOrWhiteSpace(name)) continue;

                items.Add(new UpdateItem
                {
                    Id               = name,
                    Title            = name,
                    Version          = ver,
                    AvailableVersion = newVer,
                    Provider         = Name,
                });
            }
            Logger.Info($"[Chocolatey] {items.Count} mise(s) à jour détectée(s)");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Chocolatey] Scan: {ex.Message}"); }
        return items;
    }

    public async Task<UpdateResult> InstallAsync(
        List<UpdateItem>   items,
        IProgress<string>? progress = null,
        CancellationToken  ct       = default)
    {
        if (!IsAvailable) return new UpdateResult(false, "Chocolatey non disponible");

        progress?.Report("Mise à jour de tous les paquets Chocolatey...");
        try
        {
            var output  = await RunAsync("choco", "upgrade all -y --no-color --accept-license", progress, ct);
            bool success = !output.Contains("Failures", StringComparison.OrdinalIgnoreCase);
            Logger.Info("[Chocolatey] Upgrade terminé");
            return new UpdateResult(success, success ? "Réussi" : "Erreurs détectées", items.Count(i => i.IsSelected));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Chocolatey] Install: {ex.Message}"); return new UpdateResult(false, ex.Message); }
    }

    // --- ISelfManagedProvider ---

    public bool CanInstallSelf   => !IsAvailable;
    public bool CanUninstallSelf => IsAvailable;

    public async Task<bool> InstallSelfAsync(IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report("Installation de Chocolatey...");
        Logger.Info("[Chocolatey] Auto-installation...");
        try
        {
            var script = "Set-ExecutionPolicy Bypass -Scope Process -Force; " +
                         "[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; " +
                         "iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))";

            var output = await RunAsync("powershell.exe",
                $"-NoProfile -NonInteractive -Command \"{script}\"", progress, ct);

            _available = null; // force re-check
            bool ok = IsAvailable;
            Logger.Info($"[Chocolatey] Installation: {(ok ? "OK" : "Échec")}");
            return ok;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Chocolatey] InstallSelf: {ex.Message}"); return false; }
    }

    public async Task<bool> UninstallSelfAsync(IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report("Désinstallation de Chocolatey...");
        Logger.Info("[Chocolatey] Auto-désinstallation...");
        try
        {
            // Remove choco folder and PATH entry
            var script = @"
$chocoPath = Join-Path $env:ProgramData 'chocolatey'
if (Test-Path $chocoPath) { Remove-Item $chocoPath -Recurse -Force }
$machine = [Environment]::GetEnvironmentVariable('PATH','Machine')
$machine = ($machine -split ';' | Where-Object { $_ -notlike '*chocolatey*' }) -join ';'
[Environment]::SetEnvironmentVariable('PATH', $machine, 'Machine')
";
            await RunAsync("powershell.exe",
                $"-NoProfile -NonInteractive -Command \"{script.Replace("\"", "\\\"")}\"", progress, ct);

            _available = null;
            Logger.Info("[Chocolatey] Désinstallation terminée");
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Chocolatey] UninstallSelf: {ex.Message}"); return false; }
    }

    public async Task<UninstallResult> UninstallPackagesAsync(
        List<HistoryItem>  items,
        IProgress<string>? progress,
        CancellationToken  ct)
    {
        if (!IsAvailable) return new UninstallResult(false, "Chocolatey non disponible");

        int uninstalled = 0, failed = 0;
        var errors = new List<string>();

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Désinstallation: {item.Title}...");
            try
            {
                var (output, exitCode) = await RunWithCodeAsync("choco",
                    $"uninstall {item.Id} -y --no-color",
                    progress, ct);

                bool ok = exitCode == 0 && !output.Contains("Failures", StringComparison.OrdinalIgnoreCase);
                if (ok) { uninstalled++; Logger.Info($"[Chocolatey] Désinstallé: {item.Title}"); }
                else { failed++; errors.Add(item.Title); Logger.Warn($"[Chocolatey] Échec désinstall {item.Title}: exit {exitCode}"); }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { failed++; errors.Add(item.Title); Logger.Error($"[Chocolatey] Désinstall {item.Title}: {ex.Message}"); }
        }

        return new UninstallResult(failed == 0, $"{uninstalled} désinstallé(s), {failed} erreur(s)", uninstalled, failed, errors);
    }

    public async Task<List<HistoryItem>> GetInstalledPackagesAsync(CancellationToken ct = default)
    {
        var items = new List<HistoryItem>();
        if (!IsAvailable) return items;

        try
        {
            var output = await RunAsync("choco", "list --no-color -r", ct: ct);
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length < 2) continue;
                string name = parts[0].Trim();
                string ver  = parts[1].Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                items.Add(new HistoryItem
                {
                    Title    = name,
                    Id       = name,
                    Version  = ver,
                    Provider = "Chocolatey",
                    Status   = HistoryStatus.Success,
                    Date     = DateTime.MinValue,
                });
            }
            Logger.Info($"[Chocolatey] {items.Count} paquets installés");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Chocolatey] GetInstalled: {ex.Message}"); }
        return items;
    }
}
