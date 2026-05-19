using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PureUpdate.Core.Models;
using PureUpdate.Core.Providers;
using PureUpdate.Utils;

namespace PureUpdate.UI.ViewModels;

public partial class ProviderCardViewModel : ObservableObject
{
    private readonly IUpdateProvider _provider;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private bool   _isScanning;
    [ObservableProperty] private bool   _isInstalling;
    [ObservableProperty] private string _statusText   = "Prêt";
    [ObservableProperty] private string _progressText = string.Empty;

    public string Name        => _provider.Name;
    public string Description => _provider.Description;
    public string Icon        => _provider.Icon;
    public bool   IsAvailable => _provider.IsAvailable;

    public ObservableCollection<UpdateItem> Updates { get; } = [];

    public int UpdateCount => Updates.Count;

    public ProviderCardViewModel(IUpdateProvider provider)
    {
        _provider = provider;
        Updates.CollectionChanged += (_, _) => OnPropertyChanged(nameof(UpdateCount));

        if (!provider.IsAvailable)
            StatusText = "Non installé";
    }

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

            foreach (var item in found)
                Updates.Add(item);

            StatusText = found.Count > 0
                ? $"{found.Count} mise(s) à jour disponible(s)"
                : "À jour";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analyse annulée";
        }
        catch (Exception ex)
        {
            Logger.Error($"[{Name}] Scan: {ex.Message}");
            StatusText = "Erreur d'analyse";
        }
        finally
        {
            IsScanning = false;
            InstallCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    public async Task InstallAsync(CancellationToken ct = default)
    {
        if (IsInstalling || !CanInstall()) return;

        Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        IsInstalling = true;
        StatusText   = "Installation...";

        var selected = Updates.Where(u => u.IsSelected).ToList();
        var progress = new Progress<string>(msg => ProgressText = msg);

        try
        {
            var result = await _provider.InstallAsync(selected, progress, _cts.Token);

            if (result.Success)
                Updates.Clear();

            StatusText = result.Success
                ? $"{result.InstalledCount} mise(s) à jour installée(s)"
                : $"Erreurs: {result.FailedCount}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Installation annulée";
        }
        catch (Exception ex)
        {
            Logger.Error($"[{Name}] Install: {ex.Message}");
            StatusText = "Erreur d'installation";
        }
        finally
        {
            IsInstalling = false;
            ProgressText = string.Empty;
            InstallCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanInstall() => !IsInstalling && IsAvailable && Updates.Any(u => u.IsSelected);

    public void Cancel()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
