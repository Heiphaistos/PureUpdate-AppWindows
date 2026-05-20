using System.Windows;
using System.Windows.Controls;
using PureUpdate.UI.ViewModels;
using PureUpdate.Utils;

namespace PureUpdate.UI.Views;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel();
    }

    private async void BtnCleanCache_Click(object sender, RoutedEventArgs e)
    {
        BtnCleanCache.IsEnabled = false;
        TxtCleanStatus.Visibility = Visibility.Collapsed;

        int cleaned = await Task.Run(CleanWindowsCache);

        TxtCleanStatus.Text = $"{cleaned} élément(s) supprimé(s)";
        TxtCleanStatus.Visibility = Visibility.Visible;
        BtnCleanCache.IsEnabled = true;
    }

    private static int CleanWindowsCache()
    {
        int count = 0;
        var dirs = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "SoftwareDistribution", "Download"),
            Path.GetTempPath(),
        };
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
            {
                try { File.Delete(file); count++; }
                catch { }
            }
        }
        Logger.Info($"[Settings] Cache nettoyé: {count} fichier(s)");
        return count;
    }
}
