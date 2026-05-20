using System.Diagnostics;
using PureUpdate.Utils;

namespace PureUpdate.Core.Services;

public static class SchedulerService
{
    private const string TaskFolder = "PureUpdate";
    private const string TaskName   = @"PureUpdate\AutoScan";

    public static bool TaskExists()
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", $"/Query /TN \"{TaskName}\"")
            {
                CreateNoWindow         = true,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    public static Task<bool> CreateDailyTaskAsync(string exePath, string startTime = "09:00") =>
        CreateTaskAsync(exePath, "DAILY", startTime);

    public static Task<bool> CreateWeeklyTaskAsync(string exePath, string startTime = "09:00") =>
        CreateTaskAsync(exePath, "WEEKLY", startTime);

    private static async Task<bool> CreateTaskAsync(string exePath, string schedule, string startTime)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return false;

        Logger.Info($"[Scheduler] Création tâche {schedule} à {startTime}");

        return await Task.Run(() =>
        {
            try
            {
                // Create folder
                RunSilent($"/Create /F /TN \"{TaskFolder}\" /XML nul 2>nul");

                var args = $"/Create /F /SC {schedule} /TN \"{TaskName}\" " +
                           $"/TR \"\\\"{exePath}\\\" --scan\" /ST {startTime} " +
                           "/RL HIGHEST /RU SYSTEM";

                var psi = new ProcessStartInfo("schtasks.exe", args)
                {
                    CreateNoWindow         = true,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                };
                using var p = Process.Start(psi)!;
                p.WaitForExit(10_000);

                bool ok = p.ExitCode == 0;
                Logger.Info($"[Scheduler] {(ok ? "Tâche créée" : $"Échec exit={p.ExitCode}")}");
                return ok;
            }
            catch (Exception ex) { Logger.Error($"[Scheduler] Create: {ex.Message}"); return false; }
        });
    }

    public static async Task<bool> DeleteTaskAsync()
    {
        Logger.Info("[Scheduler] Suppression de la tâche...");
        return await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", $"/Delete /F /TN \"{TaskName}\"")
                {
                    CreateNoWindow  = true,
                    UseShellExecute = false,
                };
                using var p = Process.Start(psi)!;
                p.WaitForExit(5000);
                Logger.Info($"[Scheduler] Supprimé (exit {p.ExitCode})");
                return p.ExitCode == 0;
            }
            catch (Exception ex) { Logger.Error($"[Scheduler] Delete: {ex.Message}"); return false; }
        });
    }

    private static void RunSilent(string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow  = true,
                UseShellExecute = false,
            });
            p?.WaitForExit(3000);
        }
        catch { }
    }
}
