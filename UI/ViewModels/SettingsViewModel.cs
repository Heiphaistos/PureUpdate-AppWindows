using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PureUpdate.Core.Models;
using PureUpdate.Core.Services;
using PureUpdate.Utils;

namespace PureUpdate.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private AppSettings _settings;

    [ObservableProperty] private bool   _closeToTray;
    [ObservableProperty] private bool   _autoRestorePoint;
    [ObservableProperty] private string _scanSchedule     = "Disabled";
    [ObservableProperty] private bool   _taskExists;
    [ObservableProperty] private string _schedulerStatus  = string.Empty;
    [ObservableProperty] private bool   _isApplyingSchedule;

    public string ScanScheduleDisplay => ScanSchedule switch
    {
        "Daily"  => "Quotidien",
        "Weekly" => "Hebdomadaire",
        _        => "Désactivé",
    };

    public SettingsViewModel()
    {
        _settings        = AppSettingsService.Load();
        CloseToTray      = _settings.CloseToTray;
        AutoRestorePoint = _settings.AutoRestorePoint;
        ScanSchedule     = _settings.ScanSchedule;
        TaskExists       = SchedulerService.TaskExists();
        SchedulerStatus  = TaskExists ? "Tâche planifiée active" : "Aucune tâche planifiée";
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        _settings.CloseToTray = value;
        AppSettingsService.Save(_settings);
    }

    partial void OnAutoRestorePointChanged(bool value)
    {
        _settings.AutoRestorePoint = value;
        AppSettingsService.Save(_settings);
    }

    partial void OnScanScheduleChanged(string value)
    {
        OnPropertyChanged(nameof(ScanScheduleDisplay));
    }

    [RelayCommand]
    private async Task SetScheduleAsync(string? schedule)
    {
        if (schedule is null || IsApplyingSchedule) return;
        IsApplyingSchedule = true;
        ScanSchedule       = schedule;
        _settings.ScanSchedule = schedule;
        AppSettingsService.Save(_settings);

        try
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

            if (TaskExists || schedule == "Disabled")
            {
                bool del = await SchedulerService.DeleteTaskAsync();
                if (!del) Logger.Warn("[Settings] Échec suppression tâche");
                TaskExists = false;
            }

            if (schedule != "Disabled")
            {
                bool ok = schedule == "Daily"
                    ? await SchedulerService.CreateDailyTaskAsync(exePath)
                    : await SchedulerService.CreateWeeklyTaskAsync(exePath);
                TaskExists      = ok;
                SchedulerStatus = ok ? $"Tâche {ScanScheduleDisplay} créée avec succès" : "Échec de la création de tâche";
            }
            else
            {
                SchedulerStatus = "Planification automatique désactivée";
            }
        }
        finally
        {
            IsApplyingSchedule = false;
        }
    }
}
