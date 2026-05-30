using PureUpdate.UI.ViewModels;

namespace PureUpdate.UI.Views;

public partial class DriversPage : System.Windows.Controls.Page
{
    public DriversPage()
    {
        InitializeComponent();
        DataContext = new DriversViewModel();
    }
}
