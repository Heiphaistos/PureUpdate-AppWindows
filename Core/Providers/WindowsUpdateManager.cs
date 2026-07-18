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
                // Sans filtre Type : inclut logicielles ET pilotes, y compris les facultatives
                dynamic result = searcher.Search("IsInstalled=0 and IsHidden=0");

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

    /// <summary>
    /// Délai maximal par mise à jour Windows (téléchargement + installation).
    /// Surchargeable via PUREUPDATE_WU_TIMEOUT_MIN.
    /// </summary>
    private static readonly TimeSpan WuItemTimeout =
        int.TryParse(Environment.GetEnvironmentVariable("PUREUPDATE_WU_TIMEOUT_MIN"), out int m) && m > 0
            ? TimeSpan.FromMinutes(m)
            : TimeSpan.FromMinutes(60);

    public async Task<UpdateResult> InstallAsync(
        List<UpdateItem>   items,
        IProgress<string>? progress = null,
        CancellationToken  ct       = default)
    {
        // Une mise à jour à la fois (téléchargement PUIS installation par item) :
        // l'ancien Download() global de toute la collection était un appel COM bloquant
        // de plusieurs Go sans progression ni timeout — perçu comme un gel complet.
        int installed = 0, failed = 0;
        var errors     = new List<string>();
        var errorCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var wanted = await Task.Run(() =>
        {
            var list = new List<(string id, string title)>();
            var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")!;
            dynamic session  = Activator.CreateInstance(sessionType)!;
            dynamic searcher = session.CreateUpdateSearcher();
            searcher.Online  = true;
            dynamic sr       = searcher.Search("IsInstalled=0 and IsHidden=0");
            for (int i = 0; i < sr.Updates.Count; i++)
            {
                dynamic u = sr.Updates.Item(i);
                string id = (string)u.Identity.UpdateID;
                if (items.Any(x => x.Id == id))
                    list.Add((id, (string)u.Title));
            }
            return list;
        }, ct);

        if (wanted.Count == 0) return new UpdateResult(true, "Rien à installer");

        int idx = 0, total = wanted.Count;
        foreach (var (updateId, title) in wanted)
        {
            idx++;
            ct.ThrowIfCancellationRequested();
            progress?.Report($"[{idx}/{total}] {title}...");

            var itemTask = Task.Run(() => InstallSingleUpdate(updateId), ct);
            var winner   = await Task.WhenAny(itemTask, Task.Delay(WuItemTimeout, ct));
            if (winner != itemTask)
            {
                // Le service WU poursuit en arrière-plan ; on abandonne l'attente pour
                // ne pas geler la file. Pas de kill possible sur un appel COM in-process.
                failed++;
                errors.Add(title);
                errorCodes[title] = "TIMEOUT";
                Logger.Warn($"[WindowsUpdate] {title}: délai dépassé ({WuItemTimeout}), attente abandonnée, passage à la suivante");
                continue;
            }

            var (ok, detail) = await itemTask;
            if (ok) { installed++; Logger.Info($"[WindowsUpdate] Installée: {title}"); }
            else
            {
                failed++;
                errors.Add(title);
                if (!string.IsNullOrEmpty(detail)) errorCodes[title] = detail;
                Logger.Warn($"[WindowsUpdate] Échec {title}: {detail}");
            }
        }

        return new UpdateResult(
            failed == 0,
            $"{installed} installées, {failed} erreurs",
            installed, failed, errors,
            ErrorCodes: errorCodes.Count > 0 ? errorCodes : null);
    }

    /// <summary>Télécharge puis installe UNE mise à jour (session COM dédiée, thread MTA).</summary>
    private static (bool ok, string detail) InstallSingleUpdate(string updateId)
    {
        try
        {
            var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")!;
            dynamic session  = Activator.CreateInstance(sessionType)!;
            dynamic searcher = session.CreateUpdateSearcher();
            searcher.Online  = false; // déjà cherché en ligne juste avant
            dynamic sr = searcher.Search($"UpdateID='{updateId}'");
            if ((int)sr.Updates.Count == 0)
            {
                searcher.Online = true;
                sr = searcher.Search($"UpdateID='{updateId}'");
            }
            if ((int)sr.Updates.Count == 0) return (false, "INTROUVABLE");

            dynamic u = sr.Updates.Item(0);
            if (!(bool)u.EulaAccepted) u.AcceptEula();

            var collType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl")!;
            dynamic coll = Activator.CreateInstance(collType)!;
            coll.Add(u);

            if (!(bool)u.IsDownloaded)
            {
                dynamic dl = session.CreateUpdateDownloader();
                dl.Updates = coll;
                dynamic dr = dl.Download();
                if ((int)dr.ResultCode != 2)
                    return (false, $"DL 0x{(uint)(int)dr.HResult:X8}");
            }

            dynamic inst = session.CreateUpdateInstaller();
            inst.Updates = coll;
            dynamic ir   = inst.Install();
            dynamic r    = ir.GetUpdateResult(0);
            int code     = (int)r.ResultCode;
            return code == 2
                ? (true, "")
                : (false, $"RC{code} 0x{(uint)(int)r.HResult:X8}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
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
                    "-NoProfile -NonInteractive -Command \"[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; Get-HotFix | Select-Object HotFixID,Description,InstalledOn | ConvertTo-Csv -NoTypeInformation\"")
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
