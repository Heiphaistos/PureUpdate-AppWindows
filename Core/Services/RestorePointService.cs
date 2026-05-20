using System.Diagnostics;
using PureUpdate.Utils;

namespace PureUpdate.Core.Services;

public static class RestorePointService
{
    public static async Task<bool> CreateAsync(
        string description  = "PureUpdate — avant mises à jour",
        IProgress<string>?  progress = null,
        CancellationToken   ct       = default)
    {
        progress?.Report("Création du point de restauration...");
        Logger.Info($"[RestorePoint] Création: {description}");

        return await Task.Run(() =>
        {
            try
            {
                var script = $"Checkpoint-Computer -Description \"{description}\" -RestorePointType MODIFY_SETTINGS";
                var psi = new ProcessStartInfo("powershell.exe",
                    $"-NoProfile -NonInteractive -Command \"{script}\"")
                {
                    CreateNoWindow         = true,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                };

                using var p = Process.Start(psi) ?? throw new InvalidOperationException("Impossible de démarrer PowerShell");
                p.WaitForExit(30_000);

                bool ok = p.ExitCode == 0;
                Logger.Info($"[RestorePoint] {(ok ? "Créé avec succès" : $"Échec (exit {p.ExitCode})")}");
                progress?.Report(ok ? "Point de restauration créé" : "Échec du point de restauration");

                if (ok) NotificationService.NotifyRestorePoint(description);
                return ok;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Logger.Error($"[RestorePoint] {ex.Message}");
                progress?.Report($"Erreur: {ex.Message}");
                return false;
            }
        }, ct);
    }
}
