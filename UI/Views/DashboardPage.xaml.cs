using System.Windows.Controls;
using PureUpdate.UI.ViewModels;

namespace PureUpdate.UI.Views;

public partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        DataContext = new DashboardViewModel();
    }
}
