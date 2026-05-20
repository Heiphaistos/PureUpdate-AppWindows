using PureUpdate.Core.Models;
using PureUpdate.Utils;

namespace PureUpdate.Core.Providers;

public sealed class WindowsUpdateManager : IUpdateProvider
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

                progress?.Report("Téléchargement...");
                dynamic dl = session.CreateUpdateDownloader();
                dl.Updates = coll;
                dl.Download();

                progress?.Report("Installation...");
                dynamic inst = session.CreateUpdateInstaller();
                inst.Updates = coll;
                dynamic ir   = inst.Install();

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

    private static UpdateSeverity ParseSeverity(string? s) => s switch
    {
        "Critical"  => UpdateSeverity.Critical,
        "Important" => UpdateSeverity.Important,
        "Moderate"  => UpdateSeverity.Moderate,
        "Low"       => UpdateSeverity.Low,
        _           => UpdateSeverity.Unknown,
    };
}
