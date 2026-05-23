namespace PureUpdate.Core.Models;

public enum UpdateSeverity { Unknown, Low, Moderate, Important, Critical }
public enum UpdateStatus   { Pending, Downloading, Installing, Installed, Failed, ManualRequired }

public sealed class UpdateItem
{
    public required string Id       { get; init; }
    public required string Title    { get; init; }
    public string Version           { get; init; } = string.Empty;
    public string AvailableVersion  { get; init; } = string.Empty;
    public string Provider          { get; init; } = string.Empty;
    public UpdateSeverity Severity  { get; init; } = UpdateSeverity.Unknown;
    public long SizeBytes           { get; init; }
    public bool IsSelected          { get; set; }  = true;
    public UpdateStatus Status      { get; set; }  = UpdateStatus.Pending;

    public string SizeDisplay => SizeBytes > 0
        ? $"{SizeBytes / 1024.0 / 1024.0:F1} MB"
        : string.Empty;

    public string SeverityLabel => Severity switch
    {
        UpdateSeverity.Critical  => "Critical",
        UpdateSeverity.Important => "Important",
        UpdateSeverity.Moderate  => "Moderate",
        UpdateSeverity.Low       => "Low",
        _                        => "Unknown"
    };
}
