using PureUpdate.Core.Update;

namespace PureUpdate.UI.Views;

/// <summary>Dialogue de mise a jour : notes, progression, installation silencieuse puis relance.</summary>
public partial class UpdateWindow : Window
{
    private readonly AppUpdater _updater;
    private readonly UpdateInfo _info;
    private readonly Func<bool> _isBusy;
    private readonly bool _canInstall = AppUpdater.IsInstalledCopy();
    private CancellationTokenSource? _cts;

    private const string BusyText = "PureUpdate applique des mises à jour en ce moment : attendez la fin, puis réessayez.";

    public UpdateWindow(AppUpdater updater, UpdateInfo info, Version current, Func<bool> isBusy)
    {
        InitializeComponent();
        _updater = updater;
        _info = info;
        _isBusy = isBusy;
        TitleText.Text = "Une nouvelle version est disponible";
        VersionText.Text = $"Version installée : {current.ToString(3)}  →  nouvelle version : {info.Version.ToString(3)}";
        NotesText.Text = string.IsNullOrWhiteSpace(info.Notes) ? "Aucune note de version." : info.Notes;
        InstallButton.Content = _canInstall ? "Installer et redémarrer" : "Ouvrir la page de téléchargement";
        StatusText.Text = _canInstall
            ? "L'installation est silencieuse ; l'application se ferme puis redémarre toute seule."
            : "Cette copie est portable : téléchargez le nouvel exécutable depuis la page de la version.";
        Closing += (_, _) => _cts?.Cancel();
    }

    private void OnLater(object sender, RoutedEventArgs e) => Close();

    private async void OnInstall(object sender, RoutedEventArgs e)
    {
        if (!_canInstall)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppUpdater.ReleasePageUrl) { UseShellExecute = true });
            Close();
            return;
        }
        if (_isBusy()) { StatusText.Text = BusyText; return; }
        InstallButton.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Téléchargement et vérification de l'intégrité…";
        _cts = new CancellationTokenSource();
        try
        {
            var path = await _updater.DownloadAsync(_info, new Progress<double>(p => Progress.Value = p), _cts.Token);
            if (_isBusy()) // une installation a démarré pendant le téléchargement
            {
                StatusText.Text = BusyText;
                Progress.Visibility = Visibility.Collapsed;
                InstallButton.IsEnabled = true;
                return;
            }
            StatusText.Text = "Vérification réussie. Installation en cours…";
            AppUpdater.LaunchInstaller(path);
            App.ForceExit = true; // sinon « fermer dans la barre système » masque la fenêtre au lieu de quitter
            Application.Current.Shutdown();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is UpdateException or System.ComponentModel.Win32Exception or IOException)
        {
            // Win32Exception : UAC refusé par l'utilisateur.
            Progress.Visibility = Visibility.Collapsed;
            StatusText.Text = $"Échec de la mise à jour : {ex.Message}";
            InstallButton.IsEnabled = true;
        }
    }
}
