using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PureUpdate.Utils;

namespace PureUpdate.UI.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    [ObservableProperty] private string _filter = string.Empty;

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public LogsViewModel()
    {
        Logger.OnLog += OnLog;
    }

    private void OnLog(LogEntry entry)
    {
        Application.Current?.Dispatcher.InvokeAsync(() => Entries.Insert(0, entry));
    }

    [RelayCommand]
    private void Clear() => Entries.Clear();

    [RelayCommand]
    private void CopyAll()
    {
        var text = string.Join(Environment.NewLine, Entries.Select(e => e.Display));
        Clipboard.SetText(text);
    }

    ~LogsViewModel()
    {
        Logger.OnLog -= OnLog;
    }
}
