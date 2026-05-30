using PureUpdate.UI.ViewModels;

namespace PureUpdate.UI.Views;

public partial class ErrorsPage : System.Windows.Controls.Page
{
    private readonly ErrorsViewModel _vm;

    public ErrorsPage()
    {
        InitializeComponent();
        _vm = new ErrorsViewModel();
        DataContext = _vm;
    }

    public void Dispose() => _vm.Dispose();
}
