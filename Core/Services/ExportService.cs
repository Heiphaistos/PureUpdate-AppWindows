using System.Text;
using Microsoft.Win32;
using PureUpdate.Core.Models;
using PureUpdate.Utils;

namespace PureUpdate.Core.Services;

public static class ExportService
{
    public static async Task ExportToHtmlAsync(
        IEnumerable<HistoryItem> items,
        string? title = null)
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Exporter en HTML",
            Filter     = "Fichier HTML (*.html)|*.html",
            FileName   = $"PureUpdate_Export_{DateTime.Now:yyyyMMdd_HHmmss}",
            DefaultExt = ".html",
        };
        if (dlg.ShowDialog() != true) return;

        var html = BuildHtml(items, title ?? "Rapport PureUpdate");
        await File.WriteAllTextAsync(dlg.FileName, html, Encoding.UTF8);
        Logger.Info($"[Export] HTML: {dlg.FileName}");
    }

    public static async Task ExportToCsvAsync(IEnumerable<HistoryItem> items)
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Exporter en CSV",
            Filter     = "Fichier CSV (*.csv)|*.csv",
            FileName   = $"PureUpdate_Export_{DateTime.Now:yyyyMMdd_HHmmss}",
            DefaultExt = ".csv",
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("Date,Titre,Version,ID,Provider,Statut");
        foreach (var item in items)
        {
            string date = item.Date == DateTime.MinValue ? "" : item.Date.ToString("yyyy-MM-dd HH:mm");
            sb.AppendLine($"\"{date}\",\"{Esc(item.Title)}\",\"{Esc(item.Version)}\",\"{Esc(item.Id)}\",\"{item.Provider}\",\"{item.StatusLabel}\"");
        }

        await File.WriteAllTextAsync(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
        Logger.Info($"[Export] CSV: {dlg.FileName}");
    }

    private static string Esc(string s) => s.Replace("\"", "\"\"");

    private static string BuildHtml(IEnumerable<HistoryItem> items, string title)
    {
        var list = items.ToList();

        var rows = new StringBuilder();
        foreach (var item in list)
        {
            string date   = item.Date == DateTime.MinValue ? "—" : item.Date.ToString("dd/MM/yyyy HH:mm");
            string badge  = item.IsSuccess ? "badge-ok" : "badge-fail";
            string pbadge = ProviderBadgeClass(item.Provider);
            string safeTitle   = System.Net.WebUtility.HtmlEncode(item.Title);
            string safeVersion = System.Net.WebUtility.HtmlEncode(item.Version);
            rows.AppendLine($"<tr><td class=\"mono\">{date}</td><td>{safeTitle}</td><td class=\"mono\">{safeVersion}</td><td><span class=\"{pbadge}\">{item.Provider}</span></td><td><span class=\"{badge}\">{item.StatusLabel}</span></td></tr>");
        }

        int total   = list.Count;
        int success = list.Count(i => i.IsSuccess);
        int failed  = total - success;
        string generated = DateTime.Now.ToString("dddd d MMMM yyyy à HH:mm");
        int    year      = DateTime.Now.Year;
        var    v         = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        string version   = v is null ? "?" : $"{v.Major}.{v.Minor}.{v.Build}";

        // Use $$ raw string so CSS single-braces are literal; interpolations use {{expr}}
        return $$"""
<!DOCTYPE html>
<html lang="fr">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>{{title}}</title>
  <style>
    :root{--bg:#06091280;--card:#0c1424;--border:#141e2e;--text:#c0d8f0;--muted:#3a5a7a;--accent:#0078d4;--cyan:#00b7ff;--green:#4caf50;--red:#ef5350;--orange:#ff6900;--yellow:#ffb84a;}
    *{box-sizing:border-box;margin:0;padding:0;}
    body{background:#06091a;color:var(--text);font-family:-apple-system,Segoe UI,Roboto,sans-serif;padding:32px;}
    header{display:flex;align-items:center;gap:20px;border-bottom:1px solid var(--border);padding-bottom:20px;margin-bottom:28px;}
    .logo{font-size:26px;font-weight:800;}
    .logo span:first-child{color:#90b0d4;font-weight:300;}
    .logo span:last-child{color:var(--cyan);}
    .meta{color:var(--muted);font-size:12px;margin-top:4px;}
    .stats{display:flex;gap:16px;margin-bottom:24px;}
    .stat-card{background:var(--card);border:1px solid var(--border);border-radius:10px;padding:14px 20px;min-width:120px;}
    .stat-card .num{font-size:28px;font-weight:700;}
    .stat-card .lbl{font-size:11px;color:var(--muted);margin-top:2px;}
    .total .num{color:var(--cyan);} .ok .num{color:var(--green);} .fail .num{color:var(--red);}
    table{width:100%;border-collapse:collapse;background:var(--card);border:1px solid var(--border);border-radius:10px;overflow:hidden;}
    thead tr{background:#0a1220;}
    th{color:var(--muted);font-size:10.5px;text-transform:uppercase;padding:10px 14px;text-align:left;font-weight:600;}
    td{padding:9px 14px;border-bottom:1px solid var(--border);font-size:12.5px;vertical-align:middle;}
    tr:last-child td{border-bottom:none;}
    tr:hover td{background:#0d1830;}
    .mono{font-family:Consolas,monospace;font-size:11px;color:var(--muted);}
    .badge-ok{background:#1a4cff50;color:#60d090;padding:2px 9px;border-radius:5px;font-size:10.5px;font-weight:600;}
    .badge-fail{background:#3f1a1a;color:#ff7070;padding:2px 9px;border-radius:5px;font-size:10.5px;font-weight:600;}
    .p-wu{background:#0078d420;color:#4aA0f0;padding:2px 8px;border-radius:5px;font-size:10px;}
    .p-wg{background:#00b7ff20;color:var(--cyan);padding:2px 8px;border-radius:5px;font-size:10px;}
    .p-ch{background:#ff690020;color:var(--orange);padding:2px 8px;border-radius:5px;font-size:10px;}
    .p-sc{background:#4caf5020;color:var(--green);padding:2px 8px;border-radius:5px;font-size:10px;}
    footer{margin-top:24px;color:var(--muted);font-size:11px;text-align:center;}
  </style>
</head>
<body>
  <header>
    <div>
      <div class="logo"><span>Pure</span><span>Update</span></div>
      <div class="meta">Rapport généré le {{generated}}</div>
    </div>
  </header>
  <div class="stats">
    <div class="stat-card total"><div class="num">{{total}}</div><div class="lbl">Total</div></div>
    <div class="stat-card ok"><div class="num">{{success}}</div><div class="lbl">Réussis</div></div>
    <div class="stat-card fail"><div class="num">{{failed}}</div><div class="lbl">Échecs</div></div>
  </div>
  <table>
    <thead>
      <tr>
        <th>Date</th><th>Titre / Paquet</th><th>Version</th><th>Source</th><th>Statut</th>
      </tr>
    </thead>
    <tbody>
      {{rows}}
    </tbody>
  </table>
  <footer>PureUpdate v{{version}} · Rapport automatique · {{year}}</footer>
</body>
</html>
""";
    }

    private static string ProviderBadgeClass(string provider) => provider switch
    {
        "Windows Update" => "p-wu",
        "Winget"         => "p-wg",
        "Chocolatey"     => "p-ch",
        "Scoop"          => "p-sc",
        _                => "p-wu",
    };
}
