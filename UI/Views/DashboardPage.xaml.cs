using System.Windows.Controls;
using System.Windows.Threading;
using PureUpdate.Core.Services;
using PureUpdate.UI.ViewModels;

namespace PureUpdate.UI.Views;

public partial class DashboardPage : Page
{
    private readonly DashboardViewModel _vm;

    public DashboardPage()
    {
        InitializeComponent();
        _vm         = new DashboardViewModel();
        DataContext = _vm;
        Loaded     += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        var settings = AppSettingsService.Load();
        if (!settings.ScanOnStartup) return;

        // Scan différé pour laisser l'UI s'afficher d'abord
        Dispatcher.InvokeAsync(() => _vm.ScanAllCommand.Execute(null),
            DispatcherPriority.Background);
    }
}
