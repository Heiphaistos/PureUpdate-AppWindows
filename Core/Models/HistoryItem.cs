namespace PureUpdate.Core.Models;

public enum HistoryStatus { Success, SuccessWithWarnings, Failed, Aborted, Unknown }

public sealed record HistoryItem
{
    public required string Title    { get; init; }
    public DateTime        Date     { get; init; }
    public HistoryStatus   Status   { get; init; }
    public string          Provider { get; init; } = string.Empty;
    public string          Version  { get; init; } = string.Empty;
    public string          Id       { get; init; } = string.Empty;

    public string StatusLabel => Status switch
    {
        HistoryStatus.Success             => "Réussi",
        HistoryStatus.SuccessWithWarnings => "Réussi (avert.)",
        HistoryStatus.Failed              => "Échoué",
        HistoryStatus.Aborted             => "Abandonné",
        _                                 => "Inconnu",
    };

    public bool IsSuccess => Status is HistoryStatus.Success or HistoryStatus.SuccessWithWarnings;
}
