using PureUpdate.UI.ViewModels;

namespace PureUpdate.UI.Views;

public partial class UninstallPage : System.Windows.Controls.Page
{
    public UninstallPage()
    {
        InitializeComponent();
        DataContext = new UninstallViewModel();
    }
}
