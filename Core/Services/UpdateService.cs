using System.Net.Http;
using System.Reflection;
using System.Windows.Threading;
using PureUpdate.Core.Update;
using PureUpdate.UI.Views;
using PureUpdate.Utils;

namespace PureUpdate.Core.Services;

/// <summary>Planifie les verifications de mise a jour de l'application (demarrage + toutes les 6 h) et affiche le dialogue.</summary>
public sealed class UpdateService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly AppUpdater _updater;
    private readonly Version _current;
    private readonly Func<bool> _isBusy;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromHours(6) };
    private string? _declined; // version refusee : pas de rappel automatique avant la prochaine
    private bool _checking;

    /// <param name="isBusy">True tant que PureUpdate applique des mises a jour Windows/paquets : on n'installe rien alors.</param>
    public UpdateService(Func<bool> isBusy)
    {
        _isBusy = isBusy;
        _current = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PureUpdate-Updater");
        _updater = new AppUpdater(_http, _current);
        _timer.Tick += (_, _) => { if (AutoEnabled) _ = CheckAsync(false); };
    }

    private static bool AutoEnabled => AppSettingsService.Load().AutoUpdateCheck;

    public void Start()
    {
        _timer.Start();
        if (AutoEnabled) _ = CheckAsync(false);
    }

    /// <summary>manual = bouton : informe aussi quand il n'y a rien ou en cas d'erreur.</summary>
    public async Task CheckAsync(bool manual)
    {
        if (_checking) return;
        _checking = true;
        try
        {
            var info = await _updater.CheckAsync();
            if (info is null)
            {
                if (manual) Info("Vous utilisez déjà la dernière version.");
                return;
            }
            if (!manual && _declined == info.Version.ToString()) return;
            if (!manual && _isBusy()) { Logger.Info("[Update] Nouvelle version différée : installation de mises à jour en cours."); return; }
            Logger.Info($"[Update] Mise à jour disponible : {info.Version}");
            var win = new UpdateWindow(_updater, info, _current, _isBusy) { Owner = Application.Current.MainWindow };
            win.ShowDialog();
            _declined = info.Version.ToString();
        }
        catch (Exception ex) when (ex is UpdateException or OperationCanceledException)
        {
            Logger.Warn($"[Update] Vérification : {ex.Message}");
            if (manual) Info($"Impossible de vérifier les mises à jour.\n\n{ex.Message}");
        }
        finally { _checking = false; }
    }

    private static void Info(string text)
        => MessageBox.Show(Application.Current.MainWindow, text, "Mise à jour de PureUpdate", MessageBoxButton.OK, MessageBoxImage.Information);

    public void Dispose()
    {
        _timer.Stop();
        _http.Dispose();
    }
}
