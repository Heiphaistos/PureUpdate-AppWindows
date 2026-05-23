using PureUpdate.Core.Models;
using PureUpdate.Utils;

namespace PureUpdate.Core.Providers;

public sealed class WingetManager : CliProviderBase, IUpdateProvider, ISelfManagedProvider
{
    public string Name        => "Winget";
    public string Description => "Windows Package Manager";
    public string AccentHex   => "#00B7FF";

    private bool? _available;
    public bool IsAvailable => _available ??= IsCommandAvailable("winget");

    public bool CheckAvailability()
    {
        _available = IsCommandAvailable("winget");
        return _available.Value;
    }

    // --- IUpdateProvider ---

    public async Task<List<UpdateItem>> ScanAsync(CancellationToken ct = default)
    {
        var items = new List<UpdateItem>();
        if (!IsAvailable) return items;

        Logger.Info("[Winget] Scan des mises à jour...");
        try
        {
            var output = await RunAsync("winget",
                "upgrade --include-unknown --accept-source-agreements --disable-interactivity",
                ct: ct);
            items = ParseUpgradeTable(output);
            Logger.Info($"[Winget] {items.Count} mise(s) à jour détectée(s)");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Winget] Scan: {ex.Message}"); }
        return items;
    }

    // Exit codes Winget indiquant qu'une installation manuelle est requise
    private static readonly HashSet<int> _manualRequiredCodes = new()
    {
        -1978335166, // UNSUPPORTED_INSTALLER_TYPE (pas de mode silencieux possible)
        -1978335132, // SYSTEM_NOT_SUPPORTED
        -1978335157, // INSTALL_BLOCKED_BY_POLICY
        -1978335154, // INSTALL_CONTACT_SUPPORT
    };

    private static bool IsManualRequired(int exitCode, string output) =>
        _manualRequiredCodes.Contains(exitCode)
        || output.Contains("InteractiveInstall",        StringComparison.OrdinalIgnoreCase)
        || output.Contains("requires user interaction", StringComparison.OrdinalIgnoreCase)
        || output.Contains("cannot be installed silently", StringComparison.OrdinalIgnoreCase)
        || output.Contains("manual",                    StringComparison.OrdinalIgnoreCase);

    public async Task<UpdateResult> InstallAsync(
        List<UpdateItem>   items,
        IProgress<string>? progress = null,
        CancellationToken  ct       = default)
    {
        if (!IsAvailable) return new UpdateResult(false, "Winget non disponible");

        int installed = 0, failed = 0, manual = 0;
        var errors       = new List<string>();
        var manualErrors = new List<string>();

        foreach (var item in items.Where(i => i.IsSelected))
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Installation: {item.Title}...");
            try
            {
                var (output, exitCode) = await RunWithCodeAsync("winget",
                    $"upgrade --id \"{item.Id}\" --silent --include-unknown --accept-package-agreements --accept-source-agreements",
                    progress, ct);

                // 0x8A150006 = SHELLEXEC_INSTALL_FAILED : retry sans --silent
                if (exitCode == -1978335226)
                {
                    Logger.Warn($"[Winget] {item.Title}: ShellExec échoué, retry sans --silent");
                    (output, exitCode) = await RunWithCodeAsync("winget",
                        $"upgrade --id \"{item.Id}\" --include-unknown --accept-package-agreements --accept-source-agreements",
                        progress, ct);
                }

                if (exitCode is 0 or 3010)
                {
                    installed++;
                }
                else if (exitCode is -1978335213 or -1978335212)
                {
                    // MS Store bloqué par politique → manuel requis
                    manual++;
                    manualErrors.Add($"{item.Title} (MS Store bloqué)");
                    Logger.Warn($"[Winget] {item.Title}: bloqué par politique MS Store — installation manuelle requise");
                }
                else if (IsManualRequired(exitCode, output))
                {
                    manual++;
                    manualErrors.Add(item.Title);
                    Logger.Warn($"[Winget] {item.Title}: exit {exitCode:X8} — installation manuelle requise");
                }
                else
                {
                    failed++;
                    errors.Add(item.Title);
                    var detail = output.Split('\n').LastOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? "";
                    Logger.Warn($"[Winget] {item.Title}: exit {exitCode:X8}{(string.IsNullOrEmpty(detail) ? "" : $" — {detail}")}");
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { failed++; errors.Add(item.Title); Logger.Error($"[Winget] {item.Title}: {ex.Message}"); }
        }

        return new UpdateResult(
            failed == 0,
            $"{installed} installées, {manual} manuelles, {failed} erreurs",
            installed, failed, errors, manual, manualErrors);
    }

    // --- ISelfManagedProvider ---

    public bool CanInstallSelf   => false; // Winget = système Windows, pas auto-installable
    public bool CanUninstallSelf => false;

    public Task<bool> InstallSelfAsync(IProgress<string>? progress, CancellationToken ct)
        => Task.FromResult(false);

    public Task<bool> UninstallSelfAsync(IProgress<string>? progress, CancellationToken ct)
        => Task.FromResult(false);

    public async Task<List<HistoryItem>> GetInstalledPackagesAsync(CancellationToken ct = default)
    {
        var items = new List<HistoryItem>();
        if (!IsAvailable) return items;

        try
        {
            var output = await RunAsync("winget",
                "list --source winget --accept-source-agreements --disable-interactivity", ct: ct);
            items = ParseListTable(output, "Winget");
            Logger.Info($"[Winget] {items.Count} paquets installés");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Logger.Error($"[Winget] GetInstalled: {ex.Message}"); }
        return items;
    }

    // --- Parsers ---

    private List<UpdateItem> ParseUpgradeTable(string output)
    {
        var items = new List<UpdateItem>();
        // Garder les lignes vides : elles délimitent les sections de tableaux
        var lines = output.Split('\n');

        for (int h = 0; h < lines.Length; h++)
        {
            var l = lines[h];
            bool hasAvail = l.Contains("Available",   StringComparison.OrdinalIgnoreCase)
                         || l.Contains("Disponible",  StringComparison.OrdinalIgnoreCase);
            bool hasVer   = l.Contains("Version",     StringComparison.OrdinalIgnoreCase);
            bool hasId    = l.Contains("Id",          StringComparison.OrdinalIgnoreCase)
                         || l.Contains("Identifiant", StringComparison.OrdinalIgnoreCase);
            if (!hasAvail || !hasVer || !hasId) continue;

            // Trouvé un en-tête → lire les positions de colonnes spécifiques à CE tableau
            if (h + 2 >= lines.Length) continue;
            var (_, idPos, verPos, availPos, srcPos) = ParseWingetHeader(lines[h]);
            if (idPos < 0 || verPos < 0 || availPos < 0) continue;

            // Parser les données jusqu'à la prochaine ligne vide (fin de section)
            for (int i = h + 2; i < lines.Length; i++)
            {
                var line = lines[i];

                // Ligne vide = fin de cette section de tableau
                if (string.IsNullOrWhiteSpace(line)) break;
                if (line.Length < verPos) break;
                if (line.TrimStart().StartsWith("upgrade", StringComparison.OrdinalIgnoreCase)) break;

                try
                {
                    string name      = idPos > 0 ? line[..idPos].Trim() : line.Trim();
                    string id        = verPos > idPos && line.Length >= verPos ? line[idPos..verPos].Trim() : "";
                    string version   = availPos > verPos && line.Length >= availPos ? line[verPos..availPos].Trim() : "";
                    string available = srcPos > availPos && line.Length >= srcPos
                        ? line[availPos..srcPos].Trim()
                        : line.Length > availPos ? line[availPos..].Trim() : "";

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id)) continue;
                    if (name.TrimStart().StartsWith('-') || id.TrimStart().StartsWith('-')) continue;
                    if (id.Any(char.IsWhiteSpace)) continue;

                    // Éviter les doublons entre tableaux
                    if (items.Any(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))) continue;

                    items.Add(new UpdateItem
                    {
                        Id               = id,
                        Title            = name,
                        Version          = version,
                        AvailableVersion = available,
                        Provider         = "Winget",
                    });
                }
                catch { }
            }
        }

        if (items.Count == 0)
            Logger.Warn("[Winget] Aucun paquet détecté dans la sortie");

        return items;
    }

    private static List<HistoryItem> ParseListTable(string output, string provider)
    {
        var items = new List<HistoryItem>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int headerIdx = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            bool hasName = l.Contains("Name", StringComparison.OrdinalIgnoreCase)
                        || l.Contains("Nom",  StringComparison.OrdinalIgnoreCase);
            bool hasVer  = l.Contains("Version", StringComparison.OrdinalIgnoreCase);
            if (hasName && hasVer) { headerIdx = i; break; }
        }
        if (headerIdx < 0 || headerIdx + 2 >= lines.Length) return items;

        string header = lines[headerIdx];
        int idPos  = FirstValid(header, "Id", "Identifiant");
        int verPos = IndexOfWord(header, "Version");
        if (idPos < 0 || verPos < 0) return items;

        for (int i = headerIdx + 2; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length < verPos) continue;

            string name    = idPos > 0 && line.Length > idPos ? line[..idPos].Trim() : line.Trim();
            string id      = verPos > idPos && line.Length >= verPos ? line[idPos..verPos].Trim() : name;
            string version = line.Length > verPos ? line[verPos..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "" : "";

            if (string.IsNullOrWhiteSpace(name)) continue;

            items.Add(new HistoryItem
            {
                Title    = name,
                Id       = id,
                Version  = version,
                Provider = provider,
                Status   = HistoryStatus.Success,
                Date     = DateTime.MinValue,
            });
        }
        return items;
    }
}
