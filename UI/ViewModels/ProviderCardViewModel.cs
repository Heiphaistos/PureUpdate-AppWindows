using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Data;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PureUpdate.Core.Models;
using PureUpdate.Core.Providers;
using PureUpdate.Core.Services;
using PureUpdate.Utils;

namespace PureUpdate.UI.ViewModels;

public partial class ProviderCardViewModel : ObservableObject
{
    private readonly IUpdateProvider       _provider;
    private readonly ISelfManagedProvider? _selfManaged;
    private CancellationTokenSource?       _cts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    private bool _isInstalling;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    private bool _isManaging;
    [ObservableProperty] private string _statusText              = "Prêt";
    [ObservableProperty] private string _progressText            = string.Empty;
    [ObservableProperty] private bool   _isAvailable;
    [ObservableProperty] private bool   _isProgressIndeterminate = true;
    [ObservableProperty] private int    _progressValue;
    [ObservableProperty] private string _progressLabel           = string.Empty;

    private static readonly Regex _progressRx =
        new(@"^\[(\d+)/(\d+)\]\s*(.*)", RegexOptions.Compiled);

    [ObservableProperty] private string _searchText = string.Empty;
    public ICollectionView FilteredUpdates { get; }

    partial void OnSearchTextChanged(string _) => FilteredUpdates.Refresh();

    public string Name        => _provider.Name;
    public string Description => _provider.Description;
    public SolidColorBrush AccentBrush { get; }

    public bool HasSelfManagement => _selfManaged is not null;
    public bool CanInstallSelf    => _selfManaged?.CanInstallSelf == true && !IsAvailable;
    public bool CanUninstallSelf  => _selfManaged?.CanUninstallSelf == true && IsAvailable;

    public ObservableCollection<UpdateItem> Updates { get; } = [];

    public int  UpdateCount  => Updates.Count;
    public int  ManualCount  => Updates.Count(u => u.Status == UpdateStatus.ManualRequired);
    public bool HasUpdates   => Updates.Count > 0;
    public bool IsActive     => IsScanning || IsInstalling || IsManaging;

    public ProviderCardViewModel(IUpdateProvider provider)
    {
        _provider    = provider;
        _selfManaged = provider as ISelfManagedProvider;
        _isAvailable = provider.IsAvailable;
        AccentBrush  = HexToBrush(provider.AccentHex);

        FilteredUpdates = CollectionViewSource.GetDefaultView(Updates);
        FilteredUpdates.Filter = obj =>
            obj is UpdateItem item &&
            (string.IsNullOrWhiteSpace(SearchText) ||
             item.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        Updates.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(UpdateCount));
            OnPropertyChanged(nameof(ManualCount));
            OnPropertyChanged(nameof(HasUpdates));
        };

        StatusText = _isAvailable ? "Prêt" : "Non installé";
    }

    // --- Scan ---

    [RelayCommand]
    public async Task ScanAsync(CancellationToken ct = default)
    {
        if (IsScanning || !IsAvailable) return;

        Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        IsScanning = true;
        StatusText = "Analyse en cours...";
        Updates.Clear();

        try
        {
            var found = await _provider.ScanAsync(_cts.Token);
            // Exclure les mises à jour masquées par l'utilisateur
            found = found.Where(i => !HiddenUpdatesStore.IsHidden(i.Id)).ToList();
            foreach (var item in found) Updates.Add(item);
            StatusText = found.Count > 0 ? $"{found.Count} mise(s) à jour" : "À jour";
        }
        catch (OperationCanceledException) { StatusText = "Annulé"; }
        catch (Exception ex) { Logger.Error($"[{Name}] Scan: {ex.Message}"); StatusText = "Erreur"; }
        finally
        {
            IsScanning = false;
            InstallCommand.NotifyCanExecuteChanged();
        }
    }

    // --- Install updates ---

    [RelayCommand(CanExecute = nameof(CanInstall))]
    public async Task InstallAsync(CancellationToken ct = default)
    {
        if (!CanInstall()) return;

        Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        IsInstalling             = true;
        StatusText               = "Installation...";
        IsProgressIndeterminate  = true;
        ProgressValue            = 0;
        ProgressLabel            = string.Empty;

        var progress = new Progress<string>(msg =>
        {
            var m = _progressRx.Match(msg);
            if (m.Success
                && int.TryParse(m.Groups[1].Value, out int cur)
                && int.TryParse(m.Groups[2].Value, out int tot)
                && tot > 0)
            {
                IsProgressIndeterminate = false;
                ProgressValue           = (int)Math.Round((double)cur / tot * 100);
                ProgressLabel           = $"{cur} / {tot}";
                ProgressText            = m.Groups[3].Value;
            }
            else
            {
                ProgressText = msg;
            }
        });

        try
        {
            var selected = Updates.Where(u => u.IsSelected).ToList();
            var result   = await _provider.InstallAsync(selected, progress, _cts.Token);

            if (result.Success && result.ManualCount == 0)
            {
                Updates.Clear();
                StatusText = $"{result.InstalledCount} installée(s)";
            }
            else
            {
                // Titres des échecs (erreurs + manuels) à garder dans la liste
                var failedTitles = new HashSet<string>(result.Errors       ?? [], StringComparer.OrdinalIgnoreCase);
                var manualTitles = new HashSet<string>(result.ManualErrors ?? [], StringComparer.OrdinalIgnoreCase);

                // Retirer les paquets installés avec succès
                var toRemove = Updates
                    .Where(u => u.IsSelected
                             && !failedTitles.Any(f => f.StartsWith(u.Title, StringComparison.OrdinalIgnoreCase))
                             && !manualTitles.Any(m => m.StartsWith(u.Title, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                foreach (var item in toRemove) Updates.Remove(item);

                // Marquer les items qui nécessitent une installation manuelle
                foreach (var item in Updates.Where(u =>
                    manualTitles.Any(m => m.StartsWith(u.Title, StringComparison.OrdinalIgnoreCase))))
                    item.Status = UpdateStatus.ManualRequired;

                OnPropertyChanged(nameof(ManualCount));

                // Construire un status text lisible
                var parts = new List<string>();
                if (result.InstalledCount > 0)
                    parts.Add($"{result.InstalledCount} installée(s)");
                if (result.ManualCount > 0)
                    parts.Add($"{result.ManualCount} manuelle(s) requise(s) : {string.Join(", ", result.ManualErrors!)}");
                if (result.FailedCount > 0)
                    parts.Add($"{result.FailedCount} erreur(s) : {string.Join(", ", result.Errors!)}");
                StatusText = parts.Count > 0 ? string.Join(" · ", parts) : "Terminé";

                // Alimenter le store d'erreurs global (visible depuis l'onglet Erreurs)
                var ts = DateTime.Now;
                foreach (var title in result.Errors ?? [])
                {
                    string code = result.ErrorCodes?.TryGetValue(title, out var c) == true ? c : "";
                    InstallErrorStore.Add(new InstallError(ts, Name, title, "Erreur d'installation", code));
                }
                foreach (var title in result.ManualErrors ?? [])
                    InstallErrorStore.Add(new InstallError(ts, Name, title, "Installation manuelle requise"));
            }
        }
        catch (OperationCanceledException) { StatusText = "Annulé"; }
        catch (Exception ex) { Logger.Error($"[{Name}] Install: {ex.Message}"); StatusText = "Erreur"; }
        finally
        {
            IsInstalling            = false;
            ProgressText            = string.Empty;
            IsProgressIndeterminate = true;
            ProgressValue           = 0;
            ProgressLabel           = string.Empty;
            InstallCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanInstall() => !IsInstalling && IsAvailable && Updates.Any(u => u.IsSelected);

    [RelayCommand]
    private void HideItem(UpdateItem item)
    {
        HiddenUpdatesStore.Hide(item.Id);
        Updates.Remove(item);
        StatusText = Updates.Count > 0 ? $"{Updates.Count} mise(s) à jour" : "À jour";
        Logger.Info($"[{Name}] Masqué: {item.Title} ({item.Id})");
    }

    // --- Install/Uninstall the provider itself ---

    [RelayCommand]
    public async Task InstallSelfAsync(CancellationToken ct = default)
    {
        if (_selfManaged is null || !_selfManaged.CanInstallSelf) return;

        IsManaging = true;
        StatusText = $"Installation de {Name}...";
        var progress = new Progress<string>(msg => ProgressText = msg);

        try
        {
            bool ok = await _selfManaged.InstallSelfAsync(progress, ct);
            IsAvailable = _provider.CheckAvailability();
            StatusText  = ok ? "Installé avec succès" : "Échec d'installation";
            OnPropertyChanged(nameof(CanInstallSelf));
            OnPropertyChanged(nameof(CanUninstallSelf));
        }
        catch (Exception ex) { Logger.Error($"[{Name}] InstallSelf: {ex.Message}"); StatusText = $"Erreur : {ex.Message}"; }
        finally { IsManaging = false; ProgressText = string.Empty; }
    }

    [RelayCommand]
    public async Task UninstallSelfAsync(CancellationToken ct = default)
    {
        if (_selfManaged is null || !_selfManaged.CanUninstallSelf) return;

        IsManaging = true;
        StatusText = $"Désinstallation de {Name}...";
        var progress = new Progress<string>(msg => ProgressText = msg);

        try
        {
            bool ok = await _selfManaged.UninstallSelfAsync(progress, ct);
            IsAvailable = _provider.CheckAvailability();
            Updates.Clear();
            StatusText = ok ? "Désinstallé" : "Échec";
            OnPropertyChanged(nameof(CanInstallSelf));
            OnPropertyChanged(nameof(CanUninstallSelf));
        }
        catch (Exception ex) { Logger.Error($"[{Name}] UninstallSelf: {ex.Message}"); StatusText = "Erreur"; }
        finally { IsManaging = false; ProgressText = string.Empty; }
    }

    public void Cancel()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private static SolidColorBrush HexToBrush(string hex)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(c);
        }
        catch { return new SolidColorBrush(Colors.Gray); }
    }
}
