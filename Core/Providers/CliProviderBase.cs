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
        var (output, _) = await RunCoreAsync(exe, args, progress, ct);
        return output;
    }

    protected static async Task<(string output, int exitCode)> RunWithCodeAsync(
        string exe, string args,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => await RunCoreAsync(exe, args, progress, ct);

    private static async Task<(string output, int exitCode)> RunCoreAsync(
        string exe, string args,
        IProgress<string>? progress,
        CancellationToken ct)
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
        try
        {
            // ConfigureAwait(false) évite le deadlock quand appelé via GetAwaiter().GetResult() depuis le dispatcher
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        return (sb.ToString(), process.ExitCode);
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
        int id        = FirstValid(header, "Id", "Identifiant");
        int version   = IndexOfWord(header, "Version");
        int available = FirstValid(header, "Available", "Disponible");
        int source    = IndexOfWord(header, "Source");
        return (name, id, version, available, source);
    }

    protected static int FirstValid(string line, params string[] words)
    {
        foreach (var w in words)
        {
            int pos = IndexOfWord(line, w);
            if (pos >= 0) return pos;
        }
        return -1;
    }

    protected static int IndexOfWord(string line, string word)
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
