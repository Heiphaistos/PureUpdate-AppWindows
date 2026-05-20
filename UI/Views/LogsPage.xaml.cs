using System.Windows.Controls;
using System.Windows.Navigation;
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

        Loaded += (_, _) =>
        {
            if (!_vm.HasLoadedOnce)
                _vm.AutoLoadAsync();
        };
    }

    internal void Dispose() => _vm.Dispose();
}
