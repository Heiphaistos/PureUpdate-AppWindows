using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PureUpdate.Core.Models;
using PureUpdate.Core.Providers;
using PureUpdate.Core.Services;
using PureUpdate.Utils;
using PureUpdate.Utils;

namespace PureUpdate.UI.ViewModels;

public partial class LogsViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] private bool   _isLoadingWu;
    [ObservableProperty] private bool   _isLoadingPkgs;
    [ObservableProperty] private bool   _isLoadingErrors;
    [ObservableProperty] private string _wuStatus     = "Cliquez sur Actualiser";
    [ObservableProperty] private string _pkgsStatus   = "Cliquez sur Actualiser";
    [ObservableProperty] private string _errorsStatus = "Cliquez sur Charger";

    public ObservableCollection<HistoryItem>  WuHistory       { get; } = [];
    public ObservableCollection<HistoryItem>  WingetPackages  { get; } = [];
    public ObservableCollection<HistoryItem>  ChocoPackages   { get; } = [];
    public ObservableCollection<HistoryItem>  ScoopPackages   { get; } = [];
    public ObservableCollection<InstallError> InstallErrors   { get; } = [];
    public ObservableCollection<LogEntry>     AppLogs         { get; } = [];

    private readonly WingetManager _winget = new();
    private readonly ChocoManager  _choco  = new();
    private readonly ScoopManager  _scoop  = new();

    public bool HasLoadedOnce { get; private set; }

    public LogsViewModel()
    {
        Logger.OnLog += OnAppLog;
        InstallErrorStore.OnError += OnInstallError;
    }

    private void OnInstallError(InstallError error)
    {
        Application.Current?.Dispatcher.InvokeAsync(() => InstallErrors.Insert(0, error));
    }

    public void AutoLoadAsync()
    {
        HasLoadedOnce = true;
        // Fire-and-forget volontaire : la page gère ses propres états de chargement
        _ = LoadWuHistoryAsync();
        _ = LoadPackagesAsync();
    }

    private void OnAppLog(LogEntry entry)
    {
        Application.Current?.Dispatcher.InvokeAsync(() => AppLogs.Insert(0, entry));
    }

    // --- Windows Update History ---

    [RelayCommand]
    public async Task LoadWuHistoryAsync(CancellationToken ct = default)
    {
        IsLoadingWu = true;
        WuStatus    = "Chargement...";
        WuHistory.Clear();

        try
        {
            var items = await WindowsUpdateHistoryService.GetHistoryAsync(1000, ct);
            foreach (var item in items) WuHistory.Add(item);
            WuStatus = $"{items.Count} entrée(s) depuis l'installation de Windows";
        }
        catch (OperationCanceledException) { WuStatus = "Annulé"; }
        catch (Exception ex) { Logger.Error($"[WU History] {ex.Message}"); WuStatus = "Erreur"; }
        finally { IsLoadingWu = false; }
    }

    // --- Installed packages ---

    [RelayCommand]
    public async Task LoadPackagesAsync(CancellationToken ct = default)
    {
        IsLoadingPkgs = true;
        PkgsStatus    = "Chargement...";
        WingetPackages.Clear();
        ChocoPackages.Clear();
        ScoopPackages.Clear();

        try
        {
            var results = await Task.WhenAll(
                _winget.IsAvailable ? _winget.GetInstalledPackagesAsync(ct) : Task.FromResult(new List<HistoryItem>()),
                _choco.IsAvailable  ? _choco.GetInstalledPackagesAsync(ct)  : Task.FromResult(new List<HistoryItem>()),
                _scoop.IsAvailable  ? _scoop.GetInstalledPackagesAsync(ct)  : Task.FromResult(new List<HistoryItem>())
            );
            var (winget, choco, scoop) = (results[0], results[1], results[2]);

            foreach (var i in winget) WingetPackages.Add(i);
            foreach (var i in choco)  ChocoPackages.Add(i);
            foreach (var i in scoop)  ScoopPackages.Add(i);

            int total = winget.Count + choco.Count + scoop.Count;
            PkgsStatus = $"{total} paquet(s) au total — Winget: {winget.Count} | Choco: {choco.Count} | Scoop: {scoop.Count}";
        }
        catch (OperationCanceledException) { PkgsStatus = "Annulé"; }
        catch (Exception ex) { Logger.Error($"[Packages] {ex.Message}"); PkgsStatus = "Erreur"; }
        finally { IsLoadingPkgs = false; }
    }

    // --- Install errors ---

    [RelayCommand]
    public async Task LoadErrorsAsync(CancellationToken ct = default)
    {
        IsLoadingErrors = true;
        ErrorsStatus    = "Chargement...";
        InstallErrors.Clear();

        try
        {
            // 1. Erreurs de session en cours (depuis InstallErrorStore)
            var sessionErrors = InstallErrorStore.All.ToList();

            // 2. Erreurs Windows Update (charge l'historique si pas encore fait)
            if (WuHistory.Count == 0)
                await LoadWuHistoryAsync(ct);

            var wuFailed = WuHistory
                .Where(h => !h.IsSuccess)
                .Select(h => new InstallError(h.Date, "Windows Update", h.Title, h.StatusLabel));

            // 3. Erreurs des sessions précédentes depuis le fichier log
            var logPath = Path.Combine(AppContext.BaseDirectory, ".logs", "pureupdate.log");
            var logErrors = File.Exists(logPath)
                ? await ParseLogErrorsAsync(logPath, ct)
                : [];

            // Fusionner tout, dédupliquer les session errors déjà présents, trier par date décroissante
            var existing = new HashSet<(string, string)>(
                sessionErrors.Select(e => (e.Provider, e.Title)),
                EqualityComparer<(string, string)>.Default);

            var historical = wuFailed
                .Concat(logErrors)
                .Where(e => !existing.Contains((e.Provider, e.Title)))
                .OrderByDescending(e => e.Date);

            var all = sessionErrors
                .Concat(historical)
                .OrderByDescending(e => e.Date)
                .ToList();

            foreach (var e in all) InstallErrors.Add(e);

            ErrorsStatus = InstallErrors.Count > 0
                ? $"{InstallErrors.Count} erreur(s) trouvée(s)"
                : "Aucune erreur d'installation enregistrée";
        }
        catch (OperationCanceledException) { ErrorsStatus = "Annulé"; }
        catch (Exception ex) { Logger.Error($"[Errors] {ex.Message}"); ErrorsStatus = "Erreur de chargement"; }
        finally { IsLoadingErrors = false; }
    }

    private static async Task<List<InstallError>> ParseLogErrorsAsync(string logPath, CancellationToken ct)
    {
        var errors = new List<InstallError>();
        string[] lines;
        try { lines = await File.ReadAllLinesAsync(logPath, System.Text.Encoding.UTF8, ct); }
        catch { return errors; }

        foreach (var line in lines)
        {
            if (!line.StartsWith('[')) continue;

            int t1 = line.IndexOf(']');
            if (t1 < 0 || t1 + 3 >= line.Length) continue;
            if (!DateTime.TryParse(line[1..t1], out var date)) continue;

            int t2 = line.IndexOf('[', t1 + 1);
            int t3 = t2 >= 0 ? line.IndexOf(']', t2 + 1) : -1;
            if (t2 < 0 || t3 < 0) continue;

            var level = line[(t2 + 1)..t3].Trim();
            if (level is not ("WARN" or "ERROR")) continue;

            var message = line[(t3 + 2)..].Trim();

            string provider;
            if      (message.StartsWith("[Winget]",  StringComparison.OrdinalIgnoreCase)) provider = "Winget";
            else if (message.StartsWith("[Choco]",   StringComparison.OrdinalIgnoreCase)) provider = "Chocolatey";
            else if (message.StartsWith("[Scoop]",   StringComparison.OrdinalIgnoreCase)) provider = "Scoop";
            else if (message.StartsWith("[WU",       StringComparison.OrdinalIgnoreCase)) provider = "Windows Update";
            else continue;

            int pEnd  = message.IndexOf(']');
            var rest  = pEnd >= 0 ? message[(pEnd + 1)..].TrimStart() : message;
            int colon = rest.IndexOf(':');
            var title  = colon > 0 ? rest[..colon].Trim() : rest.Trim();
            var detail = colon > 0 ? rest[(colon + 1)..].Trim() : "";

            if (string.IsNullOrWhiteSpace(title)) continue;
            errors.Add(new InstallError(date, provider, title, detail));
        }

        return errors;
    }

    // --- Export ---

    [RelayCommand]
    private async Task ExportHtmlAsync()
    {
        await ExportService.ExportToHtmlAsync(WuHistory, "PureUpdate — Historique Windows Update");
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        await ExportService.ExportToCsvAsync(WuHistory);
    }

    // --- App logs ---

    [RelayCommand]
    private void ClearAppLogs() => AppLogs.Clear();

    [RelayCommand]
    private void CopyAllAppLogs()
    {
        if (AppLogs.Count == 0) return;
        var text = string.Join(Environment.NewLine, AppLogs.Select(e => e.Display));
        Clipboard.SetText(text);
    }

    public void Dispose()
    {
        Logger.OnLog              -= OnAppLog;
        InstallErrorStore.OnError -= OnInstallError;
    }
}
