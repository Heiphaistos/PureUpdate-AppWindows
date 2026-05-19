using System.Windows;
using PureUpdate.Utils;
using Wpf.Ui.Appearance;

namespace PureUpdate;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDir = Path.Combine(AppContext.BaseDirectory, ".logs");
        Logger.Initialize(logDir);

        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        if (!PrivilegeHelper.IsRunningAsAdministrator())
        {
            Logger.Warn("Application lancée sans droits administrateur");
            MessageBox.Show(
                "PureUpdate nécessite des droits Administrateur pour fonctionner correctement.\n" +
                "Certaines fonctions (Windows Update, installation de paquets) seront indisponibles.",
                "Droits insuffisants",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Shutdown();
        base.OnExit(e);
    }
}
