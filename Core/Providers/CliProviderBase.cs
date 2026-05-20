using System.Diagnostics;
using System.Text.RegularExpressions;
using PureUpdate.Utils;

namespace PureUpdate.Core.Providers;

public abstract class CliProviderBase
{
    private static readonly string FullPath =
        (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "") + ";" +
        (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User)    ?? "");

    protected static async Task<string> RunAsync(
        string exe, string args,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8,
        };
        psi.EnvironmentVariables["PATH"] = FullPath;

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var sb = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            var clean = StripAnsi(e.Data);
            sb.AppendLine(clean);
            if (!string.IsNullOrWhiteSpace(clean)) progress?.Report(clean);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            Logger.Warn($"[{exe}] {e.Data}");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct);
        return sb.ToString();
    }

    protected static bool IsCommandAvailable(string command)
    {
        try
        {
            var psi = new ProcessStartInfo(command, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            psi.EnvironmentVariables["PATH"] = FullPath;

            using var p = Process.Start(psi);
            p?.WaitForExit(3000);
            return p?.ExitCode is 0 or 1;
        }
        catch { return false; }
    }

    protected static string StripAnsi(string text) =>
        Regex.Replace(text, @"\x1B\[[0-9;]*[a-zA-Z]|\x1B\].*?\x07", "");

    protected static (int name, int id, int version, int available, int source)
        ParseWingetHeader(string header)
    {
        int name      = 0;
        int id        = IndexOfWord(header, "Id");
        int version   = IndexOfWord(header, "Version");
        int available = IndexOfWord(header, "Available");
        int source    = IndexOfWord(header, "Source");
        return (name, id, version, available, source);
    }

    private static int IndexOfWord(string line, string word)
    {
        int idx = 0;
        while (idx < line.Length)
        {
            int found = line.IndexOf(word, idx, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return -1;
            bool leftOk  = found == 0 || !char.IsLetterOrDigit(line[found - 1]);
            bool rightOk = found + word.Length >= line.Length || !char.IsLetterOrDigit(line[found + word.Length]);
            if (leftOk && rightOk) return found;
            idx = found + 1;
        }
        return -1;
    }
}
