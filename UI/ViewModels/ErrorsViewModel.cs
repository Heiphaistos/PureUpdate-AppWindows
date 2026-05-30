using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PureUpdate.Core.Models;
using PureUpdate.Core.Providers;
using PureUpdate.Core.Services;
using PureUpdate.Utils;

namespace PureUpdate.UI.ViewModels;

public partial class ErrorsViewModel : ObservableObject, IDisposable
{
    // Descriptions des codes d'erreur Windows Update les plus courants
    private static readonly Dictionary<string, string> WuErrorDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0x80070422"] = "Service Windows Update désactivé",
        ["0x8024402F"] = "Serveur inaccessible — problème réseau/proxy",
        ["0x80244022"] = "Windows Update temporairement indisponible",
        ["0x8024200D"] = "Fichier de mise à jour endommagé",
        ["0x80070005"] = "Accès refusé — droits administrateur requis",
        ["0x8007000E"] = "Mémoire insuffisante",
        ["0x80240034"] = "Téléchargement échoué (BITS)",
        ["0x80073701"] = "Fichier CBS manquant ou endommagé",
        ["0x8024000B"] = "Opération annulée",
        ["0x80240FFF"] = "Erreur inattendue de Windows Update",
        ["0x80070BC9"] = "Redémarrage requis avant installation",
        ["0x8024800C"] = "Connexion interrompue pendant la mise à jour",
        ["0xC1900101"] = "Pilote incompatible ou problème matériel",
        ["0x8007007E"] = "Fichier système manquant",
        ["0x80070103"] = "Pilote déjà à jour ou incompatible",
        ["0x80096010"] = "Signature numérique invalide",
        ["0x80248007"] = "Source de mise à jour introuvable",
        ["0x80070570"] = "Fichier ou répertoire corrompu",
        ["0x8A150006"] = "Winget — installeur sans mode silencieux (ShellExec)",
        ["0x8A15002B"] = "Winget — accord de licence requis",
        ["0x8A150011"] = "Winget — application déjà installée",
        ["0x8A15003B"] = "Winget — version introuvable dans les sources",
        ["0x8A150058"] = "Winget — bloqué par politique d'entreprise",
    };

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _statusText  = "Chargez les erreurs depuis la session courante ou l'historique";
    [ObservableProperty] private string _filterText  = string.Empty;
    [ObservableProperty] private string _activeFilter = "Tous";
    [ObservableProperty] private int    _totalCount;
    [ObservableProperty] private int    _wuCount;
    [ObservableProperty] private int    _wingetCount;
    [ObservableProperty] private int    _chocoCount;
    [ObservableProperty] private int    _scoopCount;

    public ObservableCollection<ErrorDisplayItem> AllErrors { get; } = [];
    public ICollectionView                         FilteredErrors { get; }

    public ErrorsViewModel()
    {
        FilteredErrors = CollectionViewSource.GetDefaultView(AllErrors);
        FilteredErrors.Filter = FilterItem;

        InstallErrorStore.OnError   += OnNewError;
        InstallErrorStore.OnCleared += OnCleared;

        // Charger les erreurs de session déjà présentes
        foreach (var e in InstallErrorStore.All)
            AllErrors.Add(BuildItem(e));

        RefreshCounts();
    }

    private bool FilterItem(object obj)
    {
        if (obj is not ErrorDisplayItem item) return false;
        bool providerMatch = ActiveFilter is "Tous" || item.Provider == ActiveFilter;
        bool textMatch = string.IsNullOrEmpty(FilterText)
            || item.Title.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
            || item.ErrorCode.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
            || item.ErrorMessage.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
        return providerMatch && textMatch;
    }

    partial void OnFilterTextChanged(string value)   => FilteredErrors.Refresh();
    partial void OnActiveFilterChanged(string value) => FilteredErrors.Refresh();

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading  = true;
        StatusText = "Chargement...";
        AllErrors.Clear();

        try
        {
            // 1. Erreurs de session (en mémoire)
            var session = InstallErrorStore.All.ToList();

            // 2. Historique Windows Update via WUAPI
            StatusText = "Chargement de l'historique Windows Update...";
            List<HistoryItem> wuHistory = [];
            try
            {
                wuHistory = await WindowsUpdateHistoryService.GetHistoryAsync(1000, ct);
            }
            catch (Exception ex) { Logger.Warn($"[Erreurs] WU History: {ex.Message}"); }

            var wuFailed = wuHistory
                .Where(h => !h.IsSuccess)
                .Select(h => new InstallError(h.Date, "Windows Update", h.Title, h.StatusLabel, h.ErrorCode));

            // 3. Erreurs dans les logs
            var logErrors = await ParseLogErrorsAsync(ct);

            // Fusionner sans doublons
            var sessionKeys = new HashSet<(string, string)>(session.Select(e => (e.Provider, e.Title)));
            var merged = session
                .Concat(wuFailed.Where(e => !sessionKeys.Contains((e.Provider, e.Title))))
                .Concat(logErrors.Where(e => !sessionKeys.Contains((e.Provider, e.Title))))
                .OrderByDescending(e => e.Date)
                .ToList();

            foreach (var e in merged)
                AllErrors.Add(BuildItem(e));

            RefreshCounts();
            StatusText = AllErrors.Count > 0
                ? $"{AllErrors.Count} erreur(s) trouvée(s)"
                : "Aucune erreur d'installation trouvée";
        }
        catch (OperationCanceledException) { StatusText = "Annulé"; }
        catch (Exception ex) { Logger.Error($"[Erreurs] Load: {ex.Message}"); StatusText = "Erreur de chargement"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void SetFilter(string filter)
    {
        ActiveFilter = filter;
        FilteredErrors.Refresh();
    }

    [RelayCommand]
    private void CopyError(ErrorDisplayItem? item)
    {
        if (item is null) return;
        var text = string.IsNullOrEmpty(item.ErrorCode)
            ? $"[{item.Provider}] {item.Title} — {item.ErrorMessage}"
            : $"[{item.Provider}] {item.Title} — Code: {item.ErrorCode} — {item.ErrorMessage}";
        Clipboard.SetText(text);
    }

    [RelayCommand]
    private void ClearAll()
    {
        AllErrors.Clear();
        InstallErrorStore.Clear();
        RefreshCounts();
        StatusText = "Liste vidée";
    }

    private void OnNewError(InstallError error)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            AllErrors.Insert(0, BuildItem(error));
            RefreshCounts();
        });
    }

    private void OnCleared()
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            AllErrors.Clear();
            RefreshCounts();
        });
    }

    private void RefreshCounts()
    {
        TotalCount  = AllErrors.Count;
        WuCount     = AllErrors.Count(e => e.Provider == "Windows Update");
        WingetCount = AllErrors.Count(e => e.Provider == "Winget");
        ChocoCount  = AllErrors.Count(e => e.Provider == "Chocolatey");
        ScoopCount  = AllErrors.Count(e => e.Provider == "Scoop");
        OnPropertyChanged(nameof(HasErrors));
    }

    public bool HasErrors => AllErrors.Count > 0;

    private ErrorDisplayItem BuildItem(InstallError e)
    {
        string desc = !string.IsNullOrEmpty(e.ErrorCode) && WuErrorDescriptions.TryGetValue(e.ErrorCode, out var d)
            ? d : e.ErrorMessage;
        return new ErrorDisplayItem(e.Date, e.Provider, e.Title, e.ErrorCode, e.ErrorMessage, desc,
            ProviderAccent(e.Provider));
    }

    private static string ProviderAccent(string provider) => provider switch
    {
        "Windows Update" => "#0078D4",
        "Winget"         => "#00B7FF",
        "Chocolatey"     => "#FF6900",
        "Scoop"          => "#5CB85C",
        _                => "#607080",
    };

    private static async Task<List<InstallError>> ParseLogErrorsAsync(CancellationToken ct)
    {
        var errors  = new List<InstallError>();
        var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, ".logs", "pureupdate.log");
        if (!System.IO.File.Exists(logPath)) return errors;

        string[] lines;
        try { lines = await System.IO.File.ReadAllLinesAsync(logPath, System.Text.Encoding.UTF8, ct); }
        catch { return errors; }

        foreach (var line in lines)
        {
            if (!line.StartsWith('[')) continue;
            int t1 = line.IndexOf(']');
            if (t1 < 0 || !DateTime.TryParse(line[1..t1], out var date)) continue;
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

            // Extraire le code hex si présent dans le detail (ex: "exit 8A150006")
            string code = "";
            var hexMatch = System.Text.RegularExpressions.Regex.Match(detail, @"\b([0-9A-Fa-f]{8})\b");
            if (hexMatch.Success) code = $"0x{hexMatch.Value.ToUpper()}";

            if (!string.IsNullOrWhiteSpace(title))
                errors.Add(new InstallError(date, provider, title, detail, code));
        }
        return errors;
    }

    public void Dispose()
    {
        InstallErrorStore.OnError   -= OnNewError;
        InstallErrorStore.OnCleared -= OnCleared;
    }
}

public sealed record ErrorDisplayItem(
    DateTime Date,
    string   Provider,
    string   Title,
    string   ErrorCode,
    string   ErrorMessage,
    string   Description,
    string   AccentHex);
