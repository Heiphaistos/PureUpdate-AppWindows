using CommunityToolkit.Mvvm.ComponentModel;

namespace PureUpdate.Core.Models;

public sealed partial class UninstallableItem : ObservableObject
{
    public HistoryItem Source { get; }

    public string Title    => Source.Title;
    public string Version  => Source.Version;
    public string Id       => Source.Id;
    public string Provider => Source.Provider;

    [ObservableProperty] private bool _isSelected;

    public UninstallableItem(HistoryItem source) => Source = source;
}
