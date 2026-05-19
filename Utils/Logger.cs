using System.IO;
using System.Text;

namespace PureUpdate.Utils;

public enum LogLevel { Info, Warn, Error }

public sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Message)
{
    public string LevelText => Level switch
    {
        LogLevel.Info  => "INFO",
        LogLevel.Warn  => "WARN",
        LogLevel.Error => "ERROR",
        _              => "INFO"
    };
    public string Display => $"[{Timestamp:HH:mm:ss}] [{LevelText}] {Message}";
}

public static class Logger
{
    private static readonly object _lock = new();
    private static StreamWriter? _writer;

    public static event Action<LogEntry>? OnLog;

    public static void Initialize(string logDir)
    {
        Directory.CreateDirectory(logDir);
        var archiveDir = Path.Combine(logDir, "archive");
        Directory.CreateDirectory(archiveDir);

        var path = Path.Combine(logDir, "pureupdate.log");

        if (File.Exists(path) && new FileInfo(path).Length > 1_048_576)
        {
            var archived = Path.Combine(archiveDir, $"pureupdate_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            File.Move(path, archived);
        }

        _writer = new StreamWriter(path, append: true, System.Text.Encoding.UTF8) { AutoFlush = true };
        Info("PureUpdate started");
    }

    public static void Info(string message)  => Write(LogLevel.Info, message);
    public static void Warn(string message)  => Write(LogLevel.Warn, message);
    public static void Error(string message) => Write(LogLevel.Error, message);

    private static void Write(LogLevel level, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, message);
        lock (_lock)
        {
            _writer?.WriteLine($"[{entry.Timestamp:yyyy-MM-ddTHH:mm:ss}] [{entry.LevelText}] {message}");
        }
        OnLog?.Invoke(entry);
    }

    public static void Shutdown()
    {
        Info("PureUpdate stopped");
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
