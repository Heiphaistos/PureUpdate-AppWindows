using System.Collections.ObjectModel;
using PureUpdate.Core.Providers;

namespace PureUpdate.UI.ViewModels;

public sealed class UninstallViewModel
{
    public ObservableCollection<ProviderUninstallTabViewModel> Tabs { get; } = [];

    public UninstallViewModel()
    {
        var wu    = new WindowsUpdateManager();
        var winget = new WingetManager();
        var choco  = new ChocoManager();
        var scoop  = new ScoopManager();

        Tabs.Add(new ProviderUninstallTabViewModel(wu,     wu.Name,     wu.AccentHex,     wu.IsAvailable));
        Tabs.Add(new ProviderUninstallTabViewModel(winget, winget.Name, winget.AccentHex, winget.IsAvailable));
        Tabs.Add(new ProviderUninstallTabViewModel(choco,  choco.Name,  choco.AccentHex,  choco.IsAvailable));
        Tabs.Add(new ProviderUninstallTabViewModel(scoop,  scoop.Name,  scoop.AccentHex,  scoop.IsAvailable));
    }
}
