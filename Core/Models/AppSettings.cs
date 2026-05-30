namespace PureUpdate.Core.Models;

public sealed class AppSettings
{
    public bool   CloseToTray       { get; set; } = true;
    public bool   AutoRestorePoint  { get; set; } = true;
    public string ScanSchedule      { get; set; } = "Disabled"; // Disabled | Daily | Weekly
    public bool   StartMinimized    { get; set; } = false;
    public bool   ScanOnStartup     { get; set; } = false;

    // Personnalisation
    public string ThemePreset  { get; set; } = "Deep Space";
    public string AccentColor  { get; set; } = "#00B7FF";
    public string AppBgColor   { get; set; } = "#060C18";
    public string CardBg1Color { get; set; } = "#091525";
    public string CardBg2Color { get; set; } = "#0D1C2E";
    public string FontFamily   { get; set; } = "Segoe UI";
}
