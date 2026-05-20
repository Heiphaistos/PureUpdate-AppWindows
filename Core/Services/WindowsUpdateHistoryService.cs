using PureUpdate.Core.Models;
using PureUpdate.Utils;

namespace PureUpdate.Core.Services;

public static class WindowsUpdateHistoryService
{
    public static async Task<List<HistoryItem>> GetHistoryAsync(
        int maxCount = 1000, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var items = new List<HistoryItem>();
            try
            {
                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")
                    ?? throw new InvalidOperationException("WUAPI non disponible");

                dynamic session  = Activator.CreateInstance(sessionType)!;
                dynamic searcher = session.CreateUpdateSearcher();
                int total        = searcher.GetTotalHistoryCount();

                if (total == 0) return items;

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

                    items.Add(new HistoryItem
                    {
                        Title    = (string)entry.Title,
                        Date     = (DateTime)entry.Date,
                        Status   = status,
                        Provider = "Windows Update",
                    });
                }

                Logger.Info($"[WU History] {items.Count} entrées récupérées");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Logger.Error($"[WU History] {ex.Message}"); }

            return items;
        }, ct);
    }
}
