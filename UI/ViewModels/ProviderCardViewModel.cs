using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PureUpdate.Core.Models;
using PureUpdate.Core.Providers;
using PureUpdate.Utils;

namespace PureUpdate.UI.ViewModels;

public partial class ProviderCardViewModel : ObservableObject
{
    private readonly IUpdateProvider       _provider;
    private readonly ISelfManagedProvider? _selfManaged;
    private CancellationTokenSource?       _cts;

    [ObservableProperty] private bool   _isScanning;
    [ObservableProperty] private bool   _isInstalling;
    [ObservableProperty] private bool   _isManaging;
    [ObservableProperty] private string _statusText   = "Prêt";
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private bool   _isAvailable;

    public string Name        => _provider.Name;
    public string Description => _provider.Description;
    public SolidColorBrush AccentBrush { get; }

    public bool HasSelfManagement => _selfManaged is not null;
    public bool CanInstallSelf    => _selfManaged?.CanInstallSelf == true && !IsAvailable;
    public bool CanUninstallSelf  => _selfManaged?.CanUninstallSelf == true && IsAvailable;

    public ObservableCollection<UpdateItem> Updates { get; } = [];

    public int  UpdateCount => Updates.Count;
    public bool HasUpdates  => Updates.Count > 0;

    public ProviderCardViewModel(IUpdateProvider provider)
    {
        _provider    = provider;
        _selfManaged = provider as ISelfManagedProvider;
        _isAvailable = provider.IsAvailable;
        AccentBrush  = HexToBrush(provider.AccentHex);

        Updates.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(UpdateCount));
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

        IsInstalling = true;
        StatusText   = "Installation...";
        var progress = new Progress<string>(msg => ProgressText = msg);

        try
        {
            var selected = Updates.Where(u => u.IsSelected).ToList();
            var result   = await _provider.InstallAsync(selected, progress, _cts.Token);

            if (result.Success)
            {
                Updates.Clear();
                StatusText = $"{result.InstalledCount} installée(s)";
            }
            else
            {
                // Retirer de la liste les paquets installés avec succès
                var failedTitles = new HashSet<string>(result.Errors ?? [], StringComparer.OrdinalIgnoreCase);
                var toRemove = Updates.Where(u => u.IsSelected && !failedTitles.Any(f => f.StartsWith(u.Title, StringComparison.OrdinalIgnoreCase))).ToList();
                foreach (var item in toRemove) Updates.Remove(item);

                string details = result.Errors is { Count: > 0 }
                    ? string.Join(", ", result.Errors)
                    : "";
                StatusText = result.InstalledCount > 0
                    ? $"{result.InstalledCount} installée(s), {result.FailedCount} erreur(s) : {details}"
                    : $"{result.FailedCount} erreur(s) : {details}";
            }
        }
        catch (OperationCanceledException) { StatusText = "Annulé"; }
        catch (Exception ex) { Logger.Error($"[{Name}] Install: {ex.Message}"); StatusText = "Erreur"; }
        finally
        {
            IsInstalling = false;
            ProgressText = string.Empty;
            InstallCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanInstall() => !IsInstalling && IsAvailable && Updates.Any(u => u.IsSelected);

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
        catch (Exception ex) { Logger.Error($"[{Name}] InstallSelf: {ex.Message}"); StatusText = "Erreur"; }
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
