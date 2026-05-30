using System.Diagnostics;
using System.Windows.Media;
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
    [ObservableProperty] private bool   _scanOnStartup;
    [ObservableProperty] private string _scanSchedule        = "Disabled";
    [ObservableProperty] private bool   _taskExists;
    [ObservableProperty] private string _schedulerStatus     = string.Empty;
    [ObservableProperty] private bool   _isApplyingSchedule;
    [ObservableProperty] private AppTheme  _selectedTheme   = null!;
    [ObservableProperty] private string    _selectedFontFamily = "Segoe UI";
    [ObservableProperty] private string    _customAccentHex  = string.Empty;
    [ObservableProperty] private string    _customHexStatus  = string.Empty;

    public string ScanScheduleDisplay => ScanSchedule switch
    {
        "Daily"  => "Quotidien",
        "Weekly" => "Hebdomadaire",
        _        => "Désactivé",
    };

    public IReadOnlyList<AppTheme> Themes     => ThemeService.Presets;

    public List<string> FontFamilies { get; } =
    [
        "Segoe UI",
        "Segoe UI Variable",
        "Roboto",
        "Inter",
        "Arial",
        "Calibri",
        "Consolas",
        "JetBrains Mono",
        "Fira Code",
    ];

    public SettingsViewModel()
    {
        _settings        = AppSettingsService.Load();
        CloseToTray      = _settings.CloseToTray;
        AutoRestorePoint = _settings.AutoRestorePoint;
        ScanOnStartup    = _settings.ScanOnStartup;
        ScanSchedule     = _settings.ScanSchedule;
        TaskExists       = SchedulerService.TaskExists();
        SchedulerStatus  = TaskExists ? "Tâche planifiée active" : "Aucune tâche planifiée";

        _selectedTheme = ThemeService.Presets
            .FirstOrDefault(t => t.Name == _settings.ThemePreset)
            ?? ThemeService.Presets[0];

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

    partial void OnScanOnStartupChanged(bool value)
    {
        _settings.ScanOnStartup = value;
        AppSettingsService.Save(_settings);
    }

    partial void OnScanScheduleChanged(string value)
    {
        OnPropertyChanged(nameof(ScanScheduleDisplay));
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        _settings.ThemePreset  = value.Name;
        _settings.AccentColor  = value.Accent;
        _settings.AppBgColor   = value.AppBg;
        _settings.CardBg1Color = value.CardBg1;
        _settings.CardBg2Color = value.CardBg2;
        AppSettingsService.Save(_settings);
        ThemeService.ApplyTheme(value, SelectedFontFamily);
    }

    partial void OnSelectedFontFamilyChanged(string value)
    {
        _settings.FontFamily = value;
        AppSettingsService.Save(_settings);
        ThemeService.Apply(_settings);
    }

    [RelayCommand]
    private void ApplyCustomAccent()
    {
        var hex = CustomAccentHex.Trim();
        if (!hex.StartsWith('#')) hex = "#" + hex;
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            _ = color;
            _settings.AccentColor  = hex;
            _settings.ThemePreset  = "Custom";
            AppSettingsService.Save(_settings);
            ThemeService.Apply(_settings);
            CustomHexStatus = "Couleur appliquée !";
        }
        catch
        {
            CustomHexStatus = "Hex invalide. Exemple : #FF6B35";
        }
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
