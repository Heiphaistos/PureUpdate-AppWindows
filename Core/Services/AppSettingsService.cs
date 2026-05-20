using System.Text.Json;
using PureUpdate.Core.Models;
using PureUpdate.Utils;

namespace PureUpdate.Core.Services;

public static class AppSettingsService
{
    private static readonly string _dir  = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PureUpdate");
    private static readonly string _path = Path.Combine(_dir, "settings.json");

    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            var json = File.ReadAllText(_path, Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(json, _opts) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Logger.Warn($"[Settings] Load échoué: {ex.Message}");
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, _opts), Encoding.UTF8);
        }
        catch (Exception ex) { Logger.Warn($"[Settings] Save échoué: {ex.Message}"); }
    }
}
