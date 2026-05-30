using PureUpdate.Core.Models;
using PureUpdate.Utils;

namespace PureUpdate.Core.Providers;

public sealed class WindowsUpdateManager : IUpdateProvider, IUninstallProvider
{
    public string Name        => "Windows Update";
    public string Description => "Mises à jour système via WUAPI";
    public string AccentHex   => "#0078D4";

    private bool _isAvailable = true;
    public bool IsAvailable => _isAvailable;

    public bool CheckAvailability()
    {
        _isAvailable = Type.GetTypeFromProgID("Microsoft.Update.Session") is not null;
        return _isAvailable;
    }

    public async Task<List<UpdateItem>> ScanAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var items = new List<UpdateItem>();
            try
            {
                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")
                    ?? throw new InvalidOperationException("WUAPI introuvable");

                dynamic session  = Activator.CreateInstance(sessionType)!;
                dynamic searcher = session.CreateUpdateSearcher();
                searcher.Online  = true;

                Logger.Info("[WindowsUpdate] Scan en cours...");
                dynamic result = searcher.Search("IsInstalled=0 and Type='Software' and IsHidden=0");

                for (int i = 0; i < result.Updates.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    dynamic u = result.Updates.Item(i);
                    items.Add(new UpdateItem
                    {
                        Id        = (string)u.Identity.UpdateID,
                        Title     = (string)u.Title,
                        Provider  = Name,
                        SizeBytes = (long)u.MaxDownloadSize,
                        Severity  = ParseSeverity((string?)u.MsrcSeverity),
                    });
                }
                Logger.Info($"[WindowsUpdate] {items.Count} mise(s) à jour trouvée(s)");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Logger.Error($"[WindowsUpdate] Scan: {ex.Message}"); }
            return items;
        }, ct);
    }

    public async Task<UpdateResult> InstallAsync(
        List<UpdateItem>   items,
        IProgress<string>? progress = null,
        CancellationToken  ct       = default)
    {
        return await Task.Run(() =>
        {
            int installed = 0, failed = 0;
            var errors = new List<string>();
            try
            {
                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")!;
                dynamic session = Activator.CreateInstance(sessionType)!;
                var collType    = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl")!;
                dynamic coll    = Activator.CreateInstance(collType)!;

                dynamic searcher = session.CreateUpdateSearcher();
                searcher.Online  = true;
                dynamic sr       = searcher.Search("IsInstalled=0 and Type='Software' and IsHidden=0");

                for (int i = 0; i < sr.Updates.Count; i++)
                {
                    dynamic u = sr.Updates.Item(i);
                    if (items.Any(x => x.Id == (string)u.Identity.UpdateID))
                        coll.Add(u);
                }
                if (coll.Count == 0) return new UpdateResult(true, "Rien à installer");

                progress?.Report("[1/3] Téléchargement des mises à jour...");
                dynamic dl = session.CreateUpdateDownloader();
                dl.Updates = coll;
                dl.Download();

                progress?.Report("[2/3] Installation en cours...");
                dynamic inst = session.CreateUpdateInstaller();
                inst.Updates = coll;
                dynamic ir   = inst.Install();

                progress?.Report("[3/3] Vérification des résultats...");

                for (int i = 0; i < coll.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    dynamic r = ir.GetUpdateResult(i);
                    if ((int)r.ResultCode == 2) installed++;
                    else { failed++; errors.Add($"{(string)coll.Item(i).Title}"); }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Logger.Error($"[WindowsUpdate] Install: {ex.Message}"); return new UpdateResult(false, ex.Message); }

            return new UpdateResult(failed == 0, $"{installed} installées, {failed} erreurs", installed, failed, errors);
        }, ct);
    }

    // --- IUninstallProvider ---

    public async Task<List<HistoryItem>> GetInstalledPackagesAsync(CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            var items = new List<HistoryItem>();
            try
            {
                Logger.Info("[WindowsUpdate] Scan des mises à jour installées...");
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe",
                    "-NoProfile -NonInteractive -Command \"Get-HotFix | Select-Object HotFixID,Description,InstalledOn | ConvertTo-Csv -NoTypeInformation\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };

                using var proc = System.Diagnostics.Process.Start(psi)!;
                string output = await proc.StandardOutput.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);

                bool header = true;
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (header) { header = false; continue; }
                    var parts = ParseCsvLine(line);
                    if (parts.Length < 3) continue;
                    string kb          = parts[0].Trim('"', ' ');
                    string description = parts[1].Trim('"', ' ');
                    string installedOn = parts[2].Trim('"', ' ');
                    if (string.IsNullOrWhiteSpace(kb)) continue;

                    items.Add(new HistoryItem
                    {
                        Id       = kb,
                        Title    = string.IsNullOrWhiteSpace(description) ? kb : $"{kb} — {description}",
                        Version  = installedOn,
                        Provider = Name,
                        Status   = HistoryStatus.Success,
                        Date     = DateTime.TryParse(installedOn, out var d) ? d : DateTime.MinValue,
                    });
                }
                Logger.Info($"[WindowsUpdate] {items.Count} mise(s) à jour installée(s) trouvée(s)");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Logger.Error($"[WindowsUpdate] GetInstalled: {ex.Message}"); }
            return items;
        }, ct);
    }

    public async Task<UninstallResult> UninstallPackagesAsync(
        List<HistoryItem>  items,
        IProgress<string>? progress,
        CancellationToken  ct)
    {
        return await Task.Run(async () =>
        {
            int uninstalled = 0, failed = 0;
            var errors = new List<string>();

            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                // item.Id = "KB5012345" — extraire le numéro sans le préfixe "KB"
                string kbNumber = item.Id.StartsWith("KB", StringComparison.OrdinalIgnoreCase)
                    ? item.Id[2..] : item.Id;

                progress?.Report($"Désinstallation {item.Id}...");
                Logger.Info($"[WindowsUpdate] Désinstallation {item.Id}...");
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("wusa.exe",
                        $"/uninstall /kb:{kbNumber} /quiet /norestart")
                    {
                        UseShellExecute = false,
                        CreateNoWindow  = true,
                    };
                    using var proc = System.Diagnostics.Process.Start(psi)!;
                    await proc.WaitForExitAsync(ct);

                    // 0 = succès, 3010 = succès + redémarrage requis, 2359303 = pas trouvé
                    if (proc.ExitCode is 0 or 3010)
                    {
                        uninstalled++;
                        Logger.Info($"[WindowsUpdate] {item.Id} désinstallé (code {proc.ExitCode})");
                    }
                    else
                    {
                        failed++;
                        errors.Add(item.Title);
                        Logger.Warn($"[WindowsUpdate] Échec {item.Id}: exit {proc.ExitCode}");
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { failed++; errors.Add(item.Title); Logger.Error($"[WindowsUpdate] Désinstall {item.Id}: {ex.Message}"); }
            }

            return new UninstallResult(failed == 0, $"{uninstalled} désinstallé(s), {failed} erreur(s)", uninstalled, failed, errors);
        }, ct);
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var  current  = new StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; }
            else if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
            else { current.Append(c); }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    private static UpdateSeverity ParseSeverity(string? s) => s switch
    {
        "Critical"  => UpdateSeverity.Critical,
        "Important" => UpdateSeverity.Important,
        "Moderate"  => UpdateSeverity.Moderate,
        "Low"       => UpdateSeverity.Low,
        _           => UpdateSeverity.Unknown,
    };
}
