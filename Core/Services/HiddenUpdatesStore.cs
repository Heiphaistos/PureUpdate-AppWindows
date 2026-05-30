using System.Text.Json;
using PureUpdate.Utils;

namespace PureUpdate.Core.Services;

public static class HiddenUpdatesStore
{
    private static readonly string _filePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "hidden_updates.json");

    private static readonly HashSet<string> _hiddenIds;

    static HiddenUpdatesStore()
    {
        _hiddenIds = Load();
    }

    public static bool IsHidden(string id) =>
        _hiddenIds.Contains(id);

    public static void Hide(string id)
    {
        if (_hiddenIds.Add(id)) Save();
    }

    public static void Unhide(string id)
    {
        if (_hiddenIds.Remove(id)) Save();
    }

    public static IReadOnlyCollection<string> HiddenIds => _hiddenIds;

    private static HashSet<string> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return [];
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<HashSet<string>>(json) ?? [];
        }
        catch { return []; }
    }

    private static void Save()
    {
        try { File.WriteAllText(_filePath, JsonSerializer.Serialize(_hiddenIds)); }
        catch (Exception ex) { Logger.Error($"[HiddenUpdatesStore] {ex.Message}"); }
    }
}
