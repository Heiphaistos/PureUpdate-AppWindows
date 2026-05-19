using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PureUpdate.Core.Offline;
using PureUpdate.Core.Providers;
using PureUpdate.Utils;

namespace PureUpdate.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly SnappyIntegrator _sdi;

    [ObservableProperty] private bool   _isScanningAll;
    [ObservableProperty] private bool   _isInstallingAll;
    [ObservableProperty] private bool   _isAdmin;
    [ObservableProperty] private bool   _isNetworkAvailable;
    [ObservableProperty] private string _globalStatus = "Prêt";
    [ObservableProperty] private string _sdiStatus    = string.Empty;
    [ObservableProperty] private bool   _sdiAvailable;

    public ObservableCollection<ProviderCardViewModel> Providers { get; } = [];

    public DashboardViewModel()
    {
        _sdi         = new SnappyIntegrator();
        SdiAvailable = _sdi.IsAvailable;
        SdiStatus    = _sdi.IsAvailable ? "SDI détecté (mode hors-ligne)" : "SDI non détecté";

        Providers.Add(new ProviderCardViewModel(new WindowsUpdateManager()));
        Providers.Add(new ProviderCardViewModel(new WingetManager()));
        Providers.Add(new ProviderCardViewModel(new ChocoManager()));
        Providers.Add(new ProviderCardViewModel(new ScoopManager()));

        IsAdmin             = PrivilegeHelper.IsRunningAsAdministrator();
        IsNetworkAvailable  = PrivilegeHelper.IsNetworkAvailable();

        Logger.Info($"Admin={IsAdmin} | Réseau={IsNetworkAvailable} | SDI={SdiAvailable}");
    }

    [RelayCommand]
    private async Task ScanAllAsync(CancellationToken ct)
    {
        IsScanningAll = true;
        GlobalStatus  = "Analyse de tous les gestionnaires...";

        try
        {
            await Task.WhenAll(
                Providers
                    .Where(p => p.IsAvailable)
                    .Select(p => p.ScanAsync(ct)));

            int total = Providers.Sum(p => p.UpdateCount);
            GlobalStatus = total > 0
                ? $"{total} mise(s) à jour trouvée(s) au total"
                : "Tous les paquets sont à jour";
        }
        catch (OperationCanceledException)
        {
            GlobalStatus = "Analyse annulée";
        }
        finally
        {
            IsScanningAll = false;
            InstallAllCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstallAll))]
    private async Task InstallAllAsync(CancellationToken ct)
    {
        IsInstallingAll = true;
        GlobalStatus    = "Installation en cours...";

        try
        {
            foreach (var provider in Providers.Where(p => p.IsAvailable && p.UpdateCount > 0))
            {
                ct.ThrowIfCancellationRequested();
                await provider.InstallAsync(ct);
            }

            GlobalStatus = "Toutes les mises à jour ont été installées";
        }
        catch (OperationCanceledException)
        {
            GlobalStatus = "Installation annulée";
        }
        finally
        {
            IsInstallingAll = false;
        }
    }

    private bool CanInstallAll() => !IsInstallingAll && Providers.Any(p => p.UpdateCount > 0);

    [RelayCommand]
    private async Task RunSdiAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (!SdiAvailable) return;
        SdiStatus = "Lancement de SDI...";
        var p = new Progress<string>(msg => SdiStatus = msg);
        bool ok = await _sdi.RunAsync(p, ct);
        SdiStatus = ok ? "SDI terminé avec succès" : "SDI terminé avec erreurs";
    }

    public void RefreshNetworkStatus()
    {
        IsNetworkAvailable = PrivilegeHelper.IsNetworkAvailable();
    }
}
