using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PureUpdate.Utils;

namespace PureUpdate.UI.ViewModels;

public sealed record DriverItem(
    string DeviceName,
    string DriverVersion,
    string DriverDate,
    string DriverProvider,
    string DeviceClass,
    string Status,
    bool   HasProblem,
    string InstanceId);

public partial class DriversViewModel : ObservableObject
{
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _statusText       = "Cliquez sur Analyser pour scanner les pilotes";
    [ObservableProperty] private string _searchText       = string.Empty;
    [ObservableProperty] private bool   _showProblemsOnly;

    public ObservableCollection<DriverItem> AllDrivers      { get; } = [];
    public ObservableCollection<DriverItem> FilteredDrivers { get; } = [];

    public int ProblemCount => AllDrivers.Count(d => d.HasProblem);

    partial void OnSearchTextChanged(string _)       => ApplyFilter();
    partial void OnShowProblemsOnlyChanged(bool _)   => ApplyFilter();

    [RelayCommand]
    private async Task ScanAsync(CancellationToken ct)
    {
        if (IsLoading) return;
        IsLoading  = true;
        StatusText = "Analyse des pilotes en cours...";
        AllDrivers.Clear();
        FilteredDrivers.Clear();
        OnPropertyChanged(nameof(ProblemCount));

        try
        {
            var drivers = await Task.Run(() => ScanDrivers(ct), ct);
            foreach (var d in drivers) AllDrivers.Add(d);
            ApplyFilter();
            OnPropertyChanged(nameof(ProblemCount));

            int problems = ProblemCount;
            StatusText = problems > 0
                ? $"{drivers.Count} pilote(s) — {problems} problème(s) détecté(s)"
                : $"{drivers.Count} pilote(s) — aucun problème détecté";
        }
        catch (OperationCanceledException) { StatusText = "Annulé"; }
        catch (Exception ex) { Logger.Error($"[Drivers] {ex.Message}"); StatusText = $"Erreur : {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void OpenDeviceManager() =>
        Process.Start(new ProcessStartInfo("devmgmt.msc") { UseShellExecute = true });

    private void ApplyFilter()
    {
        FilteredDrivers.Clear();
        var q = SearchText.Trim();
        foreach (var d in AllDrivers)
        {
            if (ShowProblemsOnly && !d.HasProblem) continue;
            if (!string.IsNullOrEmpty(q) &&
                !d.DeviceName.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !d.DeviceClass.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !d.DriverProvider.Contains(q, StringComparison.OrdinalIgnoreCase))
                continue;
            FilteredDrivers.Add(d);
        }
    }

    private static List<DriverItem> ScanDrivers(CancellationToken ct)
    {
        var results = new List<DriverItem>();
        try
        {
            // Get-PnpDevice returns FriendlyName, Status, Class, Manufacturer, InstanceId
            const string script =
                "Get-PnpDevice | Select-Object FriendlyName,Status,Class,Manufacturer,InstanceId,DeviceID | " +
                "ConvertTo-Csv -NoTypeInformation";

            var psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -NonInteractive -Command \"{script}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using var proc = Process.Start(psi)!;
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(30_000);

            bool header = true;
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                ct.ThrowIfCancellationRequested();
                if (header) { header = false; continue; }

                var cols = ParseCsvLine(line);
                if (cols.Length < 5) continue;

                string name     = Strip(cols[0]);
                string status   = Strip(cols[1]);
                string cls      = Strip(cols[2]);
                string mfr      = Strip(cols[3]);
                string id       = Strip(cols[4]);

                if (string.IsNullOrWhiteSpace(name)) continue;

                bool problem = !status.Equals("OK", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(status);

                results.Add(new DriverItem(
                    name, "—", "—", mfr, cls,
                    problem ? status : "OK",
                    problem, id));
            }

            Logger.Info($"[Drivers] {results.Count} pilotes, {results.Count(d => d.HasProblem)} problèmes");
        }
        catch (Exception ex) { Logger.Error($"[Drivers] ScanDrivers: {ex.Message}"); }

        return results.OrderByDescending(d => d.HasProblem).ThenBy(d => d.DeviceName).ToList();
    }

    private static string Strip(string s) => s.Trim('"', ' ', '\r');

    private static string[] ParseCsvLine(string line)
    {
        var parts = new List<string>();
        bool inQ = false;
        var cur = new StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') { inQ = !inQ; }
            else if (c == ',' && !inQ) { parts.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        parts.Add(cur.ToString());
        return [.. parts];
    }
}
