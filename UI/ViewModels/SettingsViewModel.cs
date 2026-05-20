using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PureUpdate.Core.Models;
using PureUpdate.Core.Services;
using PureUpdate.Utils;

namespace PureUpdate.UI.ViewModels;

public record AccentColorOption(string Name, string Hex)
{
    public SolidColorBrush Brush { get; } =
        new((Color)ColorConverter.ConvertFromString(Hex));
}

public partial class SettingsViewModel : ObservableObject
{
    private AppSettings _settings;

    [ObservableProperty] private bool   _closeToTray;
    [ObservableProperty] private bool   _autoRestorePoint;
    [ObservableProperty] private string _scanSchedule        = "Disabled";
    [ObservableProperty] private bool   _taskExists;
    [ObservableProperty] private string _schedulerStatus     = string.Empty;
    [ObservableProperty] private bool   _isApplyingSchedule;
    [ObservableProperty] private AccentColorOption _selectedAccentColor = null!;
    [ObservableProperty] private string            _selectedFontFamily  = "Segoe UI";

    public string ScanScheduleDisplay => ScanSchedule switch
    {
        "Daily"  => "Quotidien",
        "Weekly" => "Hebdomadaire",
        _        => "Désactivé",
    };

    public List<AccentColorOption> AccentColorOptions { get; } =
    [
        new("Cyan électrique", "#00B7FF"),
        new("Bleu Royal",      "#0078D4"),
        new("Violet",          "#9B59B6"),
        new("Émeraude",        "#2ECC71"),
        new("Orange",          "#FF8C00"),
        new("Rouge",           "#E74C3C"),
        new("Or",              "#F1C40F"),
        new("Rose",            "#E91E63"),
    ];

    public List<string> FontFamilies { get; } =
    [
        "Segoe UI",
        "Segoe UI Variable",
        "Arial",
        "Calibri",
        "Consolas",
    ];

    public SettingsViewModel()
    {
        _settings        = AppSettingsService.Load();
        CloseToTray      = _settings.CloseToTray;
        AutoRestorePoint = _settings.AutoRestorePoint;
        ScanSchedule     = _settings.ScanSchedule;
        TaskExists       = SchedulerService.TaskExists();
        SchedulerStatus  = TaskExists ? "Tâche planifiée active" : "Aucune tâche planifiée";

        _selectedAccentColor = AccentColorOptions.FirstOrDefault(o =>
            o.Hex.Equals(_settings.AccentColor, StringComparison.OrdinalIgnoreCase))
            ?? AccentColorOptions[0];

        _selectedFontFamily = FontFamilies.Contains(_settings.FontFamily)
            ? _settings.FontFamily
            : FontFamilies[0];
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

    partial void OnSelectedAccentColorChanged(AccentColorOption value)
    {
        _settings.AccentColor = value.Hex;
        AppSettingsService.Save(_settings);
        ThemeService.Apply(_settings);
    }

    partial void OnSelectedFontFamilyChanged(string value)
    {
        _settings.FontFamily = value;
        AppSettingsService.Save(_settings);
        ThemeService.Apply(_settings);
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
