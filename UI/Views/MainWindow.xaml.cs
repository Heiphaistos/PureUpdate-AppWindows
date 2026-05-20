using System.Windows;
using Wpf.Ui.Appearance;

namespace PureUpdate.UI.Views;

public partial class MainWindow : Window
{
    private DashboardPage? _dashboard;
    private LogsPage?      _logs;
    private SettingsPage?  _settings;

    public MainWindow()
    {
        InitializeComponent();
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        _dashboard = new DashboardPage();
        MainFrame.Navigate(_dashboard);
    }

    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        if (MainFrame is null) return;

        if      (sender == BtnDashboard) { _dashboard ??= new DashboardPage(); MainFrame.Navigate(_dashboard); }
        else if (sender == BtnLogs)      { _logs      ??= new LogsPage();      MainFrame.Navigate(_logs); }
        else if (sender == BtnSettings)  { _settings  ??= new SettingsPage();  MainFrame.Navigate(_settings); }
    }
}
