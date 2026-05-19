using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;

namespace PureUpdate.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        MainFrame.Navigate(new DashboardPage());
    }

    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        if (MainFrame is null) return;

        if (sender == BtnDashboard)       MainFrame.Navigate(new DashboardPage());
        else if (sender == BtnLogs)       MainFrame.Navigate(new LogsPage());
        else if (sender == BtnSettings)   MainFrame.Navigate(new SettingsPage());
    }
}
