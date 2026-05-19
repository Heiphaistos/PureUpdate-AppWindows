using System.Diagnostics;
using PureUpdate.Utils;

namespace PureUpdate.Core.Offline;

public sealed class SnappyIntegrator
{
    private readonly string _sdiPath;

    public bool IsAvailable { get; }
    public string SdiVersion { get; } = string.Empty;

    public SnappyIntegrator()
    {
        var exeDir  = AppContext.BaseDirectory;
        var sdiExe  = Path.Combine(exeDir, "SDI", "sdi64.exe");
        var sdiBat  = Path.Combine(exeDir, "SDI", "SDI_auto.bat");

        if (File.Exists(sdiExe))
        {
            _sdiPath    = sdiExe;
            IsAvailable = true;
        }
        else if (File.Exists(sdiBat))
        {
            _sdiPath    = sdiBat;
            IsAvailable = true;
        }
        else
        {
            _sdiPath    = string.Empty;
            IsAvailable = false;
        }

        if (IsAvailable)
            Logger.Info($"[SDI] Détecté: {_sdiPath}");
    }

    public async Task<bool> RunAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (!IsAvailable) return false;

        progress?.Report("Lancement de Snappy Driver Installer...");
        Logger.Info("[SDI] Lancement en mode silencieux");

        try
        {
            bool isBatch = _sdiPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
            var psi = isBatch
                ? new ProcessStartInfo("cmd.exe", $"/c \"{_sdiPath}\"")
                : new ProcessStartInfo(_sdiPath, "-autoinstall");

            psi.UseShellExecute   = false;
            psi.CreateNoWindow    = true;
            psi.WorkingDirectory  = Path.GetDirectoryName(_sdiPath)!;

            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Impossible de démarrer SDI");
            await proc.WaitForExitAsync(ct);

            Logger.Info($"[SDI] Terminé (exit code: {proc.ExitCode})");
            progress?.Report("SDI terminé");
            return proc.ExitCode == 0;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Logger.Error($"[SDI] Erreur: {ex.Message}");
            return false;
        }
    }
}
