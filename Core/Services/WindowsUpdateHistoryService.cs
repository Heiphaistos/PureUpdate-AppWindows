using PureUpdate.Core.Models;
using PureUpdate.Utils;

namespace PureUpdate.Core.Services;

public static class WindowsUpdateHistoryService
{
    public static Task<List<HistoryItem>> GetHistoryAsync(
        int maxCount = 1000, CancellationToken ct = default)
    {
        // L'API WUA (WUAPI) peut nécessiter un thread STA sur certaines configs Windows
        var tcs = new TaskCompletionSource<List<HistoryItem>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            var items = new List<HistoryItem>();
            try
            {
                ct.ThrowIfCancellationRequested();

                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                if (sessionType is null)
                {
                    Logger.Warn("[WU History] WUAPI introuvable (COM non enregistré)");
                    tcs.SetResult(items);
                    return;
                }

                dynamic session  = Activator.CreateInstance(sessionType)!;
                dynamic searcher = session.CreateUpdateSearcher();
                int total        = searcher.GetTotalHistoryCount();
                Logger.Info($"[WU History] Total historique WU : {total} entrées");

                if (total == 0) { tcs.SetResult(items); return; }

                dynamic history = searcher.QueryHistory(0, Math.Min(total, maxCount));

                for (int i = 0; i < history.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    dynamic entry = history.Item(i);
                    int code = (int)entry.ResultCode;
                    var status = code switch
                    {
                        2 => HistoryStatus.Success,
                        3 => HistoryStatus.SuccessWithWarnings,
                        4 => HistoryStatus.Failed,
                        5 => HistoryStatus.Aborted,
                        _ => HistoryStatus.Unknown,
                    };
                    string errorCode = "";
                    if (status is HistoryStatus.Failed or HistoryStatus.Aborted)
                    {
                        try
                        {
                            int hresult = (int)entry.HResult;
                            if (hresult != 0) errorCode = $"0x{(uint)hresult:X8}";
                        }
                        catch { /* HResult non disponible selon la version WUAPI */ }
                    }

                    items.Add(new HistoryItem
                    {
                        Title     = (string)entry.Title,
                        Date      = (DateTime)entry.Date,
                        Status    = status,
                        Provider  = "Windows Update",
                        ErrorCode = errorCode,
                    });
                }

                Logger.Info($"[WU History] {items.Count} entrées récupérées");
                tcs.SetResult(items);
            }
            catch (OperationCanceledException) { tcs.SetCanceled(ct); }
            catch (Exception ex)
            {
                Logger.Error($"[WU History] {ex.GetType().Name}: {ex.Message}");
                tcs.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }
}
