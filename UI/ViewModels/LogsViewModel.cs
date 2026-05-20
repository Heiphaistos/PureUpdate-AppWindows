using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PureUpdate.Core.Models;
using PureUpdate.Core.Providers;
using PureUpdate.Core.Services;
using PureUpdate.Utils;

namespace PureUpdate.UI.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    [ObservableProperty] private bool   _isLoadingWu;
    [ObservableProperty] private bool   _isLoadingPkgs;
    [ObservableProperty] private string _wuStatus   = "Cliquez sur Actualiser";
    [ObservableProperty] private string _pkgsStatus = "Cliquez sur Actualiser";

    public ObservableCollection<HistoryItem> WuHistory       { get; } = [];
    public ObservableCollection<HistoryItem> WingetPackages  { get; } = [];
    public ObservableCollection<HistoryItem> ChocoPackages   { get; } = [];
    public ObservableCollection<HistoryItem> ScoopPackages   { get; } = [];
    public ObservableCollection<LogEntry>    AppLogs         { get; } = [];

    private readonly WingetManager _winget = new();
    private readonly ChocoManager  _choco  = new();
    private readonly ScoopManager  _scoop  = new();

    public LogsViewModel()
    {
        Logger.OnLog += OnAppLog;
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
            var (winget, choco, scoop) = await Task.WhenAll(
                _winget.IsAvailable ? _winget.GetInstalledPackagesAsync(ct) : Task.FromResult(new List<HistoryItem>()),
                _choco.IsAvailable  ? _choco.GetInstalledPackagesAsync(ct)  : Task.FromResult(new List<HistoryItem>()),
                _scoop.IsAvailable  ? _scoop.GetInstalledPackagesAsync(ct)  : Task.FromResult(new List<HistoryItem>())
            ).ContinueWith(t => (t.Result[0], t.Result[1], t.Result[2]), ct);

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

    ~LogsViewModel()
    {
        Logger.OnLog -= OnAppLog;
    }
}
