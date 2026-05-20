namespace PureUpdate.Core.Models;

public sealed class AppSettings
{
    public bool   CloseToTray       { get; set; } = true;
    public bool   AutoRestorePoint  { get; set; } = true;
    public string ScanSchedule      { get; set; } = "Disabled"; // Disabled | Daily | Weekly
    public bool   StartMinimized    { get; set; } = false;

    // Personnalisation
    public string AccentColor  { get; set; } = "#00B7FF";
    public string FontFamily   { get; set; } = "Segoe UI";
}
