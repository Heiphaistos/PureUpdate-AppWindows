using PureUpdate.Core.Models;
using PureUpdate.Utils;

namespace PureUpdate.Core.Providers;

public sealed class WindowsUpdateManager : IUpdateProvider
{
    public string Name        => "Windows Update";
    public string Description => "Mises à jour du système via WUAPI";
    public string Icon        => "";
    public bool IsAvailable   => true;

    public async Task<List<UpdateItem>> ScanAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var items = new List<UpdateItem>();
            try
            {
                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")
                    ?? throw new InvalidOperationException("WUAPI introuvable sur ce système");

                dynamic session  = Activator.CreateInstance(sessionType)!;
                dynamic searcher = session.CreateUpdateSearcher();
                searcher.Online  = true;

                Logger.Info("[WindowsUpdate] Recherche en cours...");
                dynamic result = searcher.Search("IsInstalled=0 and Type='Software' and IsHidden=0");

                for (int i = 0; i < result.Updates.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    dynamic u = result.Updates.Item(i);

                    items.Add(new UpdateItem
                    {
                        Id       = (string)u.Identity.UpdateID,
                        Title    = (string)u.Title,
                        Provider = Name,
                        SizeBytes = (long)u.MaxDownloadSize,
                        Severity  = ParseSeverity((string?)u.MsrcSeverity),
                    });
                }
                Logger.Info($"[WindowsUpdate] {items.Count} mises à jour trouvées");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Logger.Error($"[WindowsUpdate] Scan échoué: {ex.Message}");
            }
            return items;
        }, ct);
    }

    public async Task<UpdateResult> InstallAsync(
        List<UpdateItem> items,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            int installed = 0;
            int failed    = 0;
            var errors    = new List<string>();

            try
            {
                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")!;
                dynamic session = Activator.CreateInstance(sessionType)!;

                var collType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl")!;
                dynamic updateColl = Activator.CreateInstance(collType)!;

                var searcher = session.CreateUpdateSearcher();
                searcher.Online = true;
                dynamic searchResult = searcher.Search("IsInstalled=0 and Type='Software' and IsHidden=0");

                for (int i = 0; i < searchResult.Updates.Count; i++)
                {
                    dynamic u = searchResult.Updates.Item(i);
                    if (items.Any(x => x.Id == (string)u.Identity.UpdateID))
                        updateColl.Add(u);
                }

                if (updateColl.Count == 0)
                    return new UpdateResult(true, "Aucune mise à jour à installer");

                progress?.Report("Téléchargement...");
                dynamic downloader = session.CreateUpdateDownloader();
                downloader.Updates = updateColl;
                downloader.Download();

                progress?.Report("Installation...");
                dynamic installer = session.CreateUpdateInstaller();
                installer.Updates = updateColl;
                dynamic installResult = installer.Install();

                for (int i = 0; i < updateColl.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    dynamic itemResult = installResult.GetUpdateResult(i);
                    if ((int)itemResult.ResultCode == 2)
                        installed++;
                    else
                    {
                        failed++;
                        errors.Add($"{(string)updateColl.Item(i).Title}: code {itemResult.HResult}");
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Logger.Error($"[WindowsUpdate] Install échoué: {ex.Message}");
                return new UpdateResult(false, ex.Message, installed, failed + 1, errors);
            }

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
