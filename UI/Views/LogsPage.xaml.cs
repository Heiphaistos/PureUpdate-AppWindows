using System.Windows.Controls;
using PureUpdate.UI.ViewModels;

namespace PureUpdate.UI.Views;

public partial class LogsPage : Page
{
    private readonly LogsViewModel _vm;

    public LogsPage()
    {
        InitializeComponent();
        _vm = new LogsViewModel();
        DataContext = _vm;
    }

    internal void Dispose() => _vm.Dispose();
}
