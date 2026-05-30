using System.Windows;
using Wpf.Ui.Appearance;

namespace PureUpdate.UI.Views;

public partial class MainWindow : Window
{
    private DashboardPage? _dashboard;
    private LogsPage?      _logs;
    private SettingsPage?  _settings;
    private UninstallPage? _uninstall;
    private ErrorsPage?    _errors;
    private DriversPage?   _drivers;

    public MainWindow()
    {
        InitializeComponent();
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        _dashboard = new DashboardPage();
        MainFrame.Navigate(_dashboard);
        Closed += (_, _) =>
        {
            _logs?.Dispose();
            _errors?.Dispose();
        };
    }

    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        if (MainFrame is null) return;

        if      (sender == BtnDashboard) { _dashboard  ??= new DashboardPage();  MainFrame.Navigate(_dashboard); }
        else if (sender == BtnErrors)    { _errors     ??= new ErrorsPage();      MainFrame.Navigate(_errors); }
        else if (sender == BtnUninstall) { _uninstall  ??= new UninstallPage();   MainFrame.Navigate(_uninstall); }
        else if (sender == BtnDrivers)   { _drivers    ??= new DriversPage();     MainFrame.Navigate(_drivers); }
        else if (sender == BtnLogs)      { _logs       ??= new LogsPage();        MainFrame.Navigate(_logs); }
        else if (sender == BtnSettings)  { _settings   ??= new SettingsPage();    MainFrame.Navigate(_settings); }
    }
}
